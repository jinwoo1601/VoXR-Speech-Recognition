# Verification fallback

How `compile-check` runs a `verification` binding that carries a Fallback — a line whose label begins `Fallback` and whose answer is not `none`.

For such a binding, `compile-check` must run the selector that line names where the binding's own order first reaches the Commands it replaces: the result the binding names for the Fallback runs the Fallback in their place, judged by its own expected green and reported under its own name, the result it names for those Commands runs them, and any other result is `BLOCKED` with the raw tail and no verdict; the selector's own exit is never a FAIL.

The selector keeps that order, never ahead of it because it picks the route: every Command the binding's order puts before the first Command the Fallback replaces runs before the selector does. A binding whose Commands 1 to 4 run in that order, with a Fallback replacing Commands 3 and 4, runs Command 1, Command 2, the selector, then the Fallback or Commands 3 and 4 as the selector's result directs.
