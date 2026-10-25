# Come contribuire

Languages: [English](CONTRIBUTING.md) | [Português](CONTRIBUTING.pt-BR.md) | Italiano

> Traduzione realizzata con l'aiuto dell'IA, non da madrelingua. Se qualcosa suona strano o sbagliato, correggetelo
> con una pull request: è un contributo molto apprezzato. In caso di dubbio, fa fede la versione inglese.

Grazie per l'aiuto. Questo progetto è un emulatore di server per Sphere. L'obiettivo è comportarsi come il gioco
originale, quindi ogni modifica al comportamento richiede una prova.

## Branch

| Branch | Scopo | Chi può scriverci |
|---|---|---|
| `main` | Base stabile. Compila e funziona sempre. | I maintainer, solo tramite pull request riviste |
| `develop` | Integrazione dei contributi accettati. | I maintainer |
| `feature/*`, `fix/*` | Il vostro lavoro, nel vostro fork. | Voi |

Aprite le pull request verso `develop`. `main` si aggiorna da `develop` dopo i test.

## Lingua

- Codice, commenti, commit, issue e pull request: **inglese**. L'autore originale li legge.
- Conversazione della comunità, guide e supporto: portoghese o italiano vanno benissimo.

## Come deve essere una buona modifica

1. **Un solo problema per pull request.** Piccola e mirata.
2. **Prove.** Per ogni cambiamento di comportamento, indicate da dove viene:
   - una cattura del server ufficiale (log dei pacchetti, senza dati di account),
   - i dati del client (`params`, `language`, moduli `.mbc`),
   - una routine decompilata del client,
   - oppure "custom": una differenza voluta rispetto all'originale, dichiarata come tale e, se è una regola del
     server, dietro un'impostazione in `appsettings.json`.
3. **Seguite il codice circostante.** Rispettate `.editorconfig` e i nomi del file. Non riformattate i file.
4. **I commenti spiegano il perché**, in breve.
5. **La build passa:** `dotnet build SphServer.sln` senza errori.
6. **Modifiche al formato dei pacchetti:** verificatele con lo schema del protocollo. Non tirate a indovinare le
   larghezze dei campi.

## Cosa non inviare

- File del gioco che non potete ridistribuire (client, modelli, texture, suoni).
- Catture di rete, `sph.db`, log o qualsiasi cosa con login, password, hash o chat.
- Segreti di qualsiasi tipo.

## Uso di assistenti IA

Benvenuti, con le stesse regole delle persone:

- Siete responsabili di ciò che inviate. Leggete il diff prima di aprire la pull request.
- L'assistente deve seguire `AGENTS.md`.
- Scrivete nella descrizione cosa avete eseguito e cosa avete visto. "Compila" non significa "l'ho provato in gioco".
- Non lasciate che un assistente inventi dettagli del protocollo. Un campo sconosciuto va segnato come sconosciuto.

## Aree che richiedono l'approvazione di un maintainer

Autenticazione e account, comandi GM e permessi, anti-cheat e validazioni, schema del database, struttura e
cifratura della rete. Aprite prima una issue.

## Revisione

Ogni pull request viene controllata con [docs/PR_REVIEW_CHECKLIST.md](docs/PR_REVIEW_CHECKLIST.md) (in inglese),
con la sicurezza al primo posto.

## Segnalare bug

Indicate: cosa avete fatto, cosa vi aspettavate, cosa è successo, le righe del log del server (`logs/server_*.log`)
e l'orario. Rimuovete prima i dati di account.
