:- consult("UnityLogic/KBs/UnityLogicAgentAPI.prolog").

belief name(warehousemanager).


add create_box && true =>
[
	act (createBox, A),
	add_agent_belief(A, has_box),
	stop
].

add deliver_box && true =>
[
	act (deliverNextBoxArea, A),
	add_agent_belief(A, has_box),
	add_agent_desire(A, call_bot),
	stop
].

add retrieve_box && true =>
[
	act (retrieveNextBoxArea, A),
	add_agent_belief(A, retrieve),
	add_agent_desire(A, call_bot),
	stop
].

add move_box && true =>
[
	act (moveNextBoxArea, A),
	add_agent_belief(A, move),
	add_agent_desire(A, call_bot),
	stop
].

add back_home && true =>
[
	cr robotsBackHome,
	stop
].