# remote-control-client

Programmazione	di	sistema 
---------------------------
Politecnico di Torino

###Introduzione
La diffusione degli elaboratori nelle loro diverse vesti (sistemi desktop e portatili, tablet, smartphone	ed altri dispositivi intelligenti) porta, sempre più spesso, a	dover	operare	in un contesto fortemente integrato, caratterizzato dall’utilizzo contemporaneo di applicativi su piattaforme differenti, ciascuna delle quali con la	propria	interfaccia utente e relative periferiche di input. Queste	ultime,	in particolare, sono fonte di	problema: se, infatti, è ragionevole avere di fronte a sé diversi monitor ed è naturale ed ergonomico rivolgere lo sguardo verso quello che contiene l’informazione	che stiamo cercando, avere più mouse e tastiere con	cui	interagire porta un livello	di	complessità	e	di inefficienza	elevato.

###Obiettivo
Alla luce	di queste	osservazioni,	si	vuole	implementare una	piattaforma	per	il	controllo	ed	il coordinamento	di	programmi	in	esecuzione	su	uno	o	più	elaboratori	remoti.	Tale	piattaforma	è	 costituita	 da una	 componente	 client, che sarà	 eseguita	 sul	 dispositivo	 con	 cui	 si	 sceglie fisicamente	di	interagire	(ovvero	quello	che	mette	a disposizione	mouse	e	tastiera)	e	da	una	componente	server,	in	esecuzione	su	tutte	le	macchine	che	si	intendono controllare.	Client	e	 server	 comunicano	 attraverso	 un	 opportuno	 protocollo	 che	 permette	 al	 client	 di	inviare	 gli	eventi	legati	al	movimento	del	mouse	ed	all’uso	della	 tastiera	ad	uno	specifico	server,	che	li tratterà	 come	 se	 fossero	 stati	 generati	 dalle	 proprie	 periferiche.	 Il	 protocollo	 permetterà	inoltre	l’invio	di	comandi	di	più	alto	livello	che	consentono la	selezione	di	un	server	tra	tutti	quelli	collegati e	l’interscambio	dei	dati	della clipboard	di	tale	server	con	l’elaboratore	client.

###Specifiche
Il	sistema	sviluppato	deve	essere	composto	da	due	moduli:	una	parte	server	e	una	parte	client. La	parte	server	dovrà:
* Mettere	a	disposizione	dell’utente	un’interfaccia	grafica	in	cui	sia	possibile	vedere	lo	stato	del	sistema	e	impostare le	opzioni	di	configurazione (porta	di	ascolto,	password,	…)
* Accettare	connessioni	in	entrata	da	un	singolo	client	alla	volta,	verificandone	le	credenziali 
* Iniettare,	nella	coda	di	sistema,	gli	eventi	ricevuti	dal	client
* Visualizzare,	in	un	modo	chiaro	ma	non	troppo	invasivo,	se	è	attualmente	il	target	del	client
* Trasferire	da	e	verso	la	propria	clipboard	i	contenuti	richiesti/inviati	dal	client
La	parte	client	dovrà:
* Mettere	a	disposizione	dell’utente	un’interfaccia	grafica	in	cui	sia	possibile	vedere	lo	stato	del	sistema, connettere/disconnettere	uno	o	più	server,	definire	un	o	più	hotkey	che	consentano	la	selezione	rapida	di	uno specifico	server	o	riabilitino	il	controllo	del	client	locale
* Intercettare	gli	eventi	mouse/tastiera	locali	ed	inviarli	al	server	attualmente	connesso
* Inviare	e	ricevere	richieste	di	aggiornamento	della	clipboard	nel	sistema	remoto.
Il	 componente	 client	 sarà	 costituito	 da	 un’applicazione	 win32	 (eventualmente	 basata	 sul framework	.NET) a	 tutto	schermo,	mentre	il	server	sarà	costituito	da	un’applicazione	win32	(o	.NET)	residente	nella	tray	area,	che	venga	attivata	automaticamente	al	login	dell’utente.
