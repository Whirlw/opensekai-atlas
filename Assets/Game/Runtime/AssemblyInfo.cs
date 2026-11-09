using System.Runtime.CompilerServices;

// The test assemblies need the internal PlaybackSettingsSnapshot constructor and a few internal
// conversion helpers. Nothing is made public for the sake of tests.
[assembly: InternalsVisibleTo("OpenSekai.Tests.EditMode")]
[assembly: InternalsVisibleTo("OpenSekai.Tests.PlayMode")]
