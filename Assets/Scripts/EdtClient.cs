using System.Collections.Generic;
using System.Threading;
using System.Net;
using Newtonsoft.Json.Linq;
using UnityEngine;
using System.IO;
using System.Text;
using System;

namespace DeltaInformatica.Elevate
{
    /// <summary>
    /// An EDT Client is responsible to connect and collect data from an EDT backend.<br/>
    /// </summary>
    /// <remarks>
    /// All the public methods are thread safe and the requests are processed in a dedicated thread<br/>
    /// <br/>
    /// <h2>How to use this class</h2>
    /// From the constructor or start method:<br/>
    /// <pre>
    /// EdtClient client = new EdtClient("host", 1234, "user", "pass");<br/>
    /// </pre>
    /// From an update method or loop:<br/>
    /// <pre>
    /// String msg = client.GetWarningMessage();<br/>
    /// if(null!= msg) Console.WriteLine(msg);<br/>
    /// String desc = client.GetDescription();<br/>
    /// if(null!=desc) Console.WriteLine(desc);<br/>
    /// </pre>
    /// From any thread or method:<br/>
    /// <pre>
    /// client.SetTag("maytag",isTagSet);<br/>
    /// </pre>
    /// Remember to stop the thread gracefully in your destructor or alike:<br/>
    /// <pre>
    /// client.Stop();<br/>
    /// </pre>
    /// </remarks>
    public class EdtClient
    {
        public static readonly String API_VERSION = "1.1";

        /// <summary>
        /// Support class for timed EDT interactions
        /// This bean is used to apply the appropriate tag when value is expired
        /// </summary>
        class Timeout
        {
            public static string FRAGMENT_PREFIX = "timeout=";
            public static string ABSOLUTE_PREFIX = "time=";

            public Timeout(float value, string tag, bool fragment = true)
            {
                this.Value = value;
                this.Tag = tag;
                this.IsFragmentTime = fragment;
            }

            public float Value { get; }
            public string Tag { get; }

            /// <summary>
            /// Tell if this timeout is referring to the fragment or it is global
            /// </summary>
            public bool IsFragmentTime { get; }

            // the following methods are used to have a consistent set for all the timeouts
            // so that timeouts with the same value are only checked in once

            public override int GetHashCode()
            {
                return Tag.GetHashCode();
            }

            public override bool Equals(object obj)
            {
                return obj is Timeout && ((Timeout)obj).Tag == Tag;
            }
        }

        /// <summary>
        /// Support class for EDT interactions
        /// </summary>
        class Situation
        {
            public Situation(string id, string[] conditions)
            {
                this.Id = id;
                this.Conditions = conditions;
            }

            public string Id { get; }
            public string[] Conditions { get; }
        }

        #region Internal Stuff
        /// <summary>
        /// The following parameters are set at creation time and never changed
        /// </summary>
        private readonly string protocol;
        private readonly string hostname;
        private readonly int port;
        private readonly string username, password;
        private readonly Thread edtThread;

        /// <summary>
        /// Set this to true to kill the internal thread
        /// </summary>
        private bool edtThreadStop;

        /// <summary>
        /// The authentication token, set when auth succeed with the given username and password
        /// </summary>
        private string accessToken;
        /// <summary>
        /// The last valid fragment id, set when a new fragment is received
        /// </summary>
        private string fragmentId;

        /// <summary>
        /// situations are updated everytime a new fragment is received
        /// </summary>
        private IList<Situation> situations = new List<Situation>(); //set by EDT when a new fragment comes in (will be overwritten)
        private ISet<Timeout> timeouts = new HashSet<Timeout>(); //set by EDT when a situation contains a timeout

        /// <summary>
        /// Dirty tags flag, used to verify if it's the case to check for situations or not
        /// </summary>
        private bool tagsDirty;
        /// <summary>
        /// Tags are just strings asserting a generic event occourred in the VR.
        /// They are matched with the conditions of the situations
        /// </summary>
        private IDictionary<string, bool> tags = new Dictionary<string, bool>(); //tags for condition matching

        #endregion

        //commands exchanged with the EDT thread --------------
        private string warningMessage; //set by EDT when a problem comes up (might be null and will be overwritten)
        private string description;    //set by EDT when a new fragment comes in (will be overwritten)

        public EdtClient(string hostname, int port, string username, string password, string protocol="https")
        {
            this.protocol = protocol;
            this.hostname = hostname;
            this.port = port;
            this.username = username;
            this.password = password;

            edtThread = new Thread(EdtWork);
            StepTime = 500;
            edtThreadStop = false;
            edtThread.Start();
            CurrentTime = 0f;
        }

        /// <summary>
        /// The time to sleep between each edt request in the working thread in milliseconds
        /// </summary>
        public int StepTime { get; set; }

        public float CurrentTime { get; set; }

        /// <summary>
        /// Consumes and return the last warning message
        /// Once retrieved the message will be destroyed on the client until a new one comes up
        /// </summary>
        /// <returns>the last warning message or null if none is available</returns>
        public string GetWarningMessage()
        {
            string m = warningMessage;
            Console.WriteLine("retrieving warning message " + warningMessage);
            lock (this)
            {
                warningMessage = null;
            }
            return m;
        }

        /// <summary>
        /// Consumes the frgament description and returns it.
        /// Once retrieve the description will be destroyed on the client until a new fragment is retrieved
        /// </summary>
        /// <returns>the last fragment description or null if none is available</returns>
        public string GetDescription()
        {
            string d = description;
            lock (this)
            {
                description = null;
            }
            return d;
        }

        /// <summary>
        /// Set a generic conditional tag to either true or false
        /// A dirty flag is set to perform situation checking asap
        /// This call is meant to be invoked from Unity and it's thread safe
        /// </summary>
        public void SetTag(string name, bool value)
        {
            lock (tags)
            {
                bool currentValue = false;
                //only set the value if the key is new or the value is different
                if(!tags.TryGetValue(name, out currentValue) || currentValue != value)
                {
                    tags[name] = value;
                    tagsDirty = true; //mark tags for situation conditions evaluation
                }
            }
        }

        /// <summary>
        /// Stops the internal communication thread.
        /// </summary>
        /// <returns>true if the thread is stopped, false if it could not stop in a given timeout</returns>
        public bool Stop(int timeout = 2000)
        {
            //stopping EDT thread gracefully
            if (!edtThreadStop)
            {
                edtThreadStop = true;
                return edtThread.Join(timeout);
            } else
            {
                return true;
            }
        }

        /// <summary>
        /// Request the current fragment at the given host
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        private String GetFragment(String hostname, int port, String accessToken)
        {
            String uri = protocol+"://" + hostname + ":" + port + "/edt-api/v1/vds/fragment";
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(uri);
            request.Method = "GET";
            request.Headers.Add("x-access-token:" + accessToken);

            StreamReader reader = new StreamReader(request.GetResponse().GetResponseStream());
            return reader.ReadToEnd();
        }

        /// <summary>
        /// Authenticate with EDT
        /// </summary>
        /// <returns>the access token to be used for the next requests</returns>
        private string Auth(string hostname, int port, string username, string password)
        {
            string uri = protocol+"://" + hostname + ":" + port + "/edt-api/v1/auth";
            HttpWebRequest authRequest = (HttpWebRequest)HttpWebRequest.Create(uri);
            authRequest.Proxy = null;
            string credentials = "{\"email\":\"" + username + "\", \"password\":\"" + password + "\"}";
            byte[] dataStream = Encoding.UTF8.GetBytes(credentials);
            authRequest.Method = "POST";
            authRequest.ContentType = "application/json";
            authRequest.ContentLength = dataStream.Length;
            try
            {
                Stream authStream = authRequest.GetRequestStream();
                authStream.Write(dataStream, 0, dataStream.Length);
                authStream.Close();

                WebResponse authResponse = authRequest.GetResponse();
                StreamReader reader = new StreamReader(authResponse.GetResponseStream());
                string jsonResponse = reader.ReadToEnd();
                JObject ar = JObject.Parse(jsonResponse);
                return ar["token"].ToString();
            } catch(Exception ex)
            {
                Console.WriteLine("Authorization failed: "+ex.Message);
                warningMessage = ex.ToString();
                return null;
            }
        }

        /// <summary>
        /// Request the current fragment at the given host
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        private String SelectNextFragment(String hostname, int port, String choiceId, String accessToken)
        {
            String uri = protocol + "://" + hostname + ":" + port + "/edt-api/v1/vds/select?choiceId=" + choiceId;
            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(uri);
            request.Method = "GET";
            request.Headers.Add("x-access-token:" + accessToken);

            StreamReader reader = new StreamReader(request.GetResponse().GetResponseStream());
            return reader.ReadToEnd();
        }

        /// <summary>
        /// Set all the situations to the choices set in the given fragment
        /// </summary>
        /// <param name="fragment"></param>
        private void UpdateSituations(JObject fragment)
        {
            //clear conditions
            situations.Clear();
            timeouts.Clear();
            //reset tags
            lock (tags)
            {
                foreach (string key in tags.Keys)
                {
                    // set the fragment timeouts to false
                    if (key.StartsWith(Timeout.FRAGMENT_PREFIX))
                    {
                        tags[key] = false;
                    }
                }
            }

            JArray choices = fragment["choices"].ToObject<JArray>();
            foreach (JObject c in choices)
            {
                JArray conditions = c["activationTags"].ToObject<JArray>();
                if (conditions.Count > 0)
                {
                    string[] ctags = new string[conditions.Count];
                    int ti = 0;
                    foreach (JObject cond in conditions)
                    {
                        string tag = cond["name"].ToString();
                        if (tag.StartsWith(Timeout.FRAGMENT_PREFIX))
                        {
                            float timeout = -1f;
                            float.TryParse(tag.Substring(Timeout.FRAGMENT_PREFIX.Length), out timeout);
                            timeouts.Add(new Timeout(timeout, tag));
                        } else if(tag.StartsWith(Timeout.ABSOLUTE_PREFIX))
                        {
                            float timeout = -1f;
                            float.TryParse(tag.Substring(Timeout.ABSOLUTE_PREFIX.Length), out timeout);
                            timeouts.Add(new Timeout(timeout, tag, false));
                        }
                        ctags[ti] = tag;
                        ++ti;
                    }
                    string cid = c["_id"].ToString();
                    situations.Add(new Situation(cid, ctags));
                }
            }
        }

        private float totalTime0;
        private float fragmentTime0;

        /// <summary>
        /// This method is running in another thread.
        /// It will regularly update the current fragment data and submit the selected choice to the EDT backend.
        /// </summary>
        private void EdtWork()
        {
            totalTime0 = CurrentTime;
            fragmentTime0 = CurrentTime;
            while (!edtThreadStop)
            {
                Thread.Sleep(StepTime);

                //verify authorized access
                if (CheckAuth())
                {
                    //retrieve current fragment
                    if (CheckFragment())
                    {
                        CheckTimeouts(CurrentTime - fragmentTime0, CurrentTime - totalTime0);
                        //if fragment is ok, let see if tags have been updated
                        if (tagsDirty)
                        {
                            CheckSituations();
                        }
                    }
                }
            }
        }

        private Boolean CheckAuth()
        {
            //verify access token
            if (null == accessToken)
            {
                Console.WriteLine("Requesting authorization");
                accessToken = Auth(hostname, port, username, password);
                if (null == accessToken)
                {
                    return false;
                }
                Console.WriteLine("Authorization granted");
            }
            return true;
        }

        private Boolean CheckFragment()
        {
            string fragmentJson = GetFragment(hostname, port, accessToken);
            if (null == fragmentJson)
            {
                warningMessage = "no fragment available";
                return false;
            }

            //raw Fragment contains status as well
            JObject rawFragment = JObject.Parse(fragmentJson);
            //extract fragment object only
            JObject fragment = rawFragment["fragment"].ToObject<JObject>();
            //XXX: do we need the status too?
            string newFragmentId = fragment["_id"].ToString();
            if (fragmentId != newFragmentId)
            {
                //update fragment time
                fragmentTime0 = CurrentTime;
                description = fragment["description"].ToString();
                fragmentId = newFragmentId;
                Console.WriteLine("Updating situations for fragment " + fragmentId);
                //add all the choices to the situations
                UpdateSituations(fragment);
                //signal story is over
                if (0 == situations.Count)
                {
                    warningMessage = "game over: no more choices available";
                }
                else
                {
                    CheckSituations();
                    return false;
                }
            }
            return true;
        }

        private void CheckTimeouts(float fragmentTime, float totalTime)
        {
            if (0 < timeouts.Count)
            {
                foreach (Timeout t in timeouts)
                {
                    // verify fragment time
                    if (t.IsFragmentTime && fragmentTime > t.Value)
                    {
                        SetTag(t.Tag, true);
                    } else
                    // verify total time
                    if (!t.IsFragmentTime && totalTime > t.Value)
                    {
                        SetTag(t.Tag, true);
                    }
                }
            }
        }

        private void CheckSituations()
        {
            //verify situations
            if (0 < situations.Count)// && 0 < tags.Count)
            {
                foreach (Situation s in situations)
                {
                    bool activate = true;
                    foreach (string c in s.Conditions)
                    {
                        bool tag = false;
                        lock (tags)
                        {
                            tags.TryGetValue(c, out tag);
                        }
                        if (!tag)
                        {
                            activate = false;
                            break; //a single failed condition is enough
                        }
                    }
                    if (activate)
                    {
                        //send the request and stop
                        Console.WriteLine("selected " + s.Id);
                        SelectNextFragment(hostname, port, s.Id, accessToken);
                        break;
                    }
                }
            }
            tagsDirty = false;
        }
    }
}
