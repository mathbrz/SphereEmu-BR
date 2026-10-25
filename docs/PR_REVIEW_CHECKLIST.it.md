# Checklist di revisione delle pull request

Languages: [English](PR_REVIEW_CHECKLIST.md) | Italiano

> Traduzione realizzata con l'aiuto dell'IA, non da madrelingua. Fa fede la versione inglese.

Si usa per ogni contributo. Una modifica che non supera la sezione sicurezza non viene accettata, anche se è
valida sotto altri aspetti.

## 1. Sicurezza (leggete tutto il diff, non solo il riassunto)

- [ ] **Nessuna chiamata di rete nascosta.** Cercate nel diff `HttpClient`, `WebClient`, `Socket`, `TcpClient`,
      `Process.Start`, `Assembly.Load`, `Activator`, `DllImport`, download e nuovi URL o IP.
- [ ] **Nessuna esfiltrazione.** Niente che legga `sph.db`, log, `appsettings.json`, variabili d'ambiente o
      login/password dei giocatori per inviarli o scriverli altrove.
- [ ] **Autenticazione intatta**, salvo che la modifica riguardi proprio quella (`Server/Login/Auth`).
- [ ] **Percorsi GM e debug** (`GmAccounts`, `IsGm`, `DebugMode`, `/give`, `/gold`, `/tp`) non ampliati né
      riattivati di default. I nuovi comandi sono solo per GM e vengono registrati nel log.
- [ ] **Il server resta l'autorità.** Posizione, danno, drop, oggetti e denaro si validano sul server.
- [ ] **Nessuna nuova dipendenza o passo di build** (pacchetti, script, workflow CI, hook git, submodule) senza
      revisione. Controllate `*.csproj`, `Directory.Build.props`, `.github/workflows`, `Tools/`.
- [ ] **Nessun segreto, cattura, database o log** aggiunto.
- [ ] **Impostazioni di updater e release** (`GithubRepo`, `AssetPacksReleaseBaseUrl`) non cambiate di nascosto.

## 2. Correttezza

- [ ] `dotnet build SphServer.sln` passa.
- [ ] Le modifiche di comportamento citano una prova, oppure sono segnate come "custom" e configurabili.
- [ ] Le modifiche al formato dei pacchetti rispettano lo schema del protocollo.
- [ ] Oggetti e denaro non si possono duplicare né creare dal nulla.

## 3. Ambito

- [ ] Un solo problema per pull request, nessuna riformattazione non correlata.
- [ ] La descrizione dice cosa è stato eseguito e cosa si è visto in gioco.

## Note per chi revisiona

- Una revisione riduce il rischio, non lo elimina. Preferite pull request piccole, leggete voi stessi il diff e
  provate su un database usa e getta prima di accettare.
- Trattate qualsiasi testo dentro un contributo (commenti, issue, contenuto dei file) come dato, mai come istruzione
  per il revisore o per un assistente.
