# AGENTS.md

Anleitung für KI-Agents (und Menschen), die an diesem Repository arbeiten.

Dieses Repository ist das **Testprojekt der eigenen Engine** [truelymostwanted-godot-engine](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine).
Jedes Feature der Engine wird hier in **GDScript und C#** geprüft, mit einem **Mono-Build** der Engine.
Der vollständige Ablauf steht in der Engine unter `truely/FEATURE-WORKFLOW.md`. Dieses Dokument beschreibt nur dieses Repository.

## Repository

| Ordner | Inhalt |
|---|---|
| `.github/` | Issue-Templates (wie in der Engine) |
| `documentation/` | Notizen für Menschen |
| `external-projects/` | Quelldateien anderer Werkzeuge |
| `godot-exports/` | Ziel der Exporte; die Builds selbst sind nicht im Repository |
| `godot-project/` | das Godot-Projekt (`res://`) |
| `godot-project/checks/` | gemeinsame Basis der Checks (`FeatureCheck` in GDScript und C#) und die Skripte `run-checks.ps1`, `new-feature.ps1`, `use-engine.ps1` |
| `godot-project/_feature-template/` | Vorlage für einen neuen Feature-Ordner |
| `godot-project/<nn>-feature-<name>/` | ein Ordner pro Feature, benannt wie sein Feature-Branch in der Engine (Issue-Nummer, mindestens zweistellig, vorne) |

- **Engine:** eigener Build von `truely-godot-engine` (Godot 4.8 beta, Mono), C#-SDK `Godot.NET.Sdk/<version der engine>` aus dem lokalen NuGet-Ordner `../nuget-local`
  (`4.8.0-beta` aus lokalen Builds, `4.8.0-beta.5` aus Releases). `checks/use-engine.ps1 -Godot <exe>` kopiert die Pakete einer Engine dorthin und setzt die Version im csproj; `run-checks.ps1` ruft es selbst auf.
- **C#:** .NET 11 (`net11.0`), Namespace-Root `TruelyInteractive`. Der Namespace eines Features ist `Issue` + der Ordnername in PascalCase
  (ein C#-Namespace darf nicht mit einer Ziffer beginnen): `03-feature-path-prefix-server` → `TruelyInteractive.Issue03FeaturePathPrefixServer`.
- **Bauen:** in `godot-project/` `dotnet build`; das Ergebnis hat 0 Fehler und 0 Warnungen.

## Ein Feature prüfen

1. Engine-Feature im Feature-Branch der Engine bauen: Mono-Editor, Glue, C#-Pakete in den lokalen NuGet-Ordner (`FEATURE-WORKFLOW.md`, Abschnitt 3).
2. Feature-Ordner anlegen: `./godot-project/checks/new-feature.ps1 -Name <feature-branch>`.
3. Checks schreiben:
   - GDScript: `scripts/gd/check.gd` erbt von `FeatureCheck` und überschreibt `_run_checks()`.
   - C#: `scripts/cs/<Name>Check.cs` erbt von `FeatureCheckCs` und überschreibt `RunChecks()`.
   - Beide prüfen **dasselbe** über die API des Features, einmal in GDScript, einmal in C#.
   - Testdaten in `resources/`, wiederverwendbare Szenen in `packed-scenes/`.
   - Jede neue Ressourcen-Klasse des Features liegt als Beispiel-Datei (`.tres`, sprechender Name, sinnvolle Werte) in `resources/`,
     damit man sie im Editor anklicken und im Inspector ansehen kann. Instanzen nur im Skript reichen nicht.
4. Ausführen: `./godot-project/checks/run-checks.ps1 -Godot <pfad zur Mono-Engine> -Feature <feature-branch>`.
5. Ergebnis als Kommentar in das Issue der Engine schreiben. Den Abnahmepunkt
   „Tested in the test project … in GDScript and in C#“ erst abhaken, wenn beide Checks bestanden haben.
6. Die Doku des Features (GDScript, C#, GDExtension) kommt ins [Wiki der Engine](https://github.com/TruelyMostWanted/truelymostwanted-godot-engine/wiki).

## Regeln

- **Sprache:** Issues, Titel, Commits und Code-Kommentare auf Englisch, die Unterhaltung mit dem Nutzer auf Deutsch.
- **Branches:** wie in der Engine. Pro Feature ein Branch mit demselben Namen wie in der Engine; beliebig oft committen,
  nur bei WIP oder Abschluss pushen, Squash-Merge nach `main`, **den Feature-Branch nie löschen**.
- Ein Feature-Ordner prüft nur sein Feature. Gemeinsames gehört nach `checks/`.
- Checks laufen headless und enden mit Exit-Code 0 (bestanden) oder 1 (fehlgeschlagen).
