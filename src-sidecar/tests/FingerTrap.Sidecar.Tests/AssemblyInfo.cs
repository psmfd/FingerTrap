using Xunit;

// Several test classes scope process-wide environment variables
// (FINGERTRAP_PI, FINGERTRAP_PANE_KIND) with set-then-restore helpers; any
// parallel execution lets one test's scope clobber another's mid-assert.
// That was a recurring 1-in-3 local false red on
// ResolvePi_EnvStillWorksWhenSettingsAreSilent and the likely mechanism
// behind CI flake #60. The whole suite runs in under half a second, so
// parallelism buys nothing worth that nondeterminism.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
