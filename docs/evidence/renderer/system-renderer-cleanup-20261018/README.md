# SystemRenderer cleanup continuation (2026-10-18)

`SystemRenderer.Dispose()` previously stopped at the first exception while
releasing timer queries, line/effect renderers, buffers or render targets. That
could leave later frame resources undisposed and make a repeated dispose retry
partially disposed objects.

The renderer now marks itself disposed once, clears the static shadow bindings,
and attempts every owned cleanup action. GPU timer query arrays are detached
before disposal and each query is attempted. Failures are reported together as
an `AggregateException` after the remaining resources have been processed.

A focused unit test verifies that the shared cleanup routine continues after a
resource throws and reports the failure. Native GL failure injection and
context-loss testing are still required to prove driver-resource recovery.
