# Render-Pass-Pflichtausgaben

Stand: 2026-10-18

`SystemRenderPassExecution.End` hat zuvor nur abgewiesen, wenn ein Pass
Ressourcen außerhalb seiner Deklaration veröffentlichte. Ein Pass konnte
deshalb als erfolgreich abgeschlossen werden, obwohl eine deklarierte
Pflichtausgabe fehlte.

Patch `2387-render-pass-required-writes.patch` prüft beim Abschluss, dass alle
deklarierten Schreibressourcen veröffentlicht wurden. Ressourcen optionaler
Color-, Depth- und Stencil-Attachments bleiben davon ausgenommen.

Die gezielten .NET-10-Tests decken eine fehlende Color-Pflichtausgabe und das
zulässige Weglassen optionaler Depth-/Stencil-Ausgaben ab: 2 bestanden, 0
fehlgeschlagen. Der Quellcode wurde kompiliert. Die Prüfung validiert den
Graph-Vertrag; sie belegt keine backendneutrale GPU-Ausführung, die weiterhin
offen ist.
