// Entry point required only by the experimental .NET web (WASM) export: the static-linked mono
// runtime needs an assembly entry point. An empty top-level statement is sufficient — the game is
// still hosted by the Godot engine, not by this Main. Compiled only in Export* configs (see
// TankGame.csproj), so desktop/Android/test builds are unaffected.
{ }
