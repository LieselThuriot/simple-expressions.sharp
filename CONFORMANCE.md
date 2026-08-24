# Conformance

Cross-language compatibility is specified and tested by the independent [simple-expressions-conformance](https://github.com/LieselThuriot/simple-expressions-conformance) repository.

The pinned `conformance/` submodule supplies `spec/v1/cases.json`, which is executed by `ConformanceTests`. Initialize it with `git submodule update --init --recursive`. The test reads the corpus through the submodule-linked test asset and does not copy files into the implementation repository.

The conformance suite is the minimum shared test set. The additional local tests cover CLR model access, caching, and port-specific semantics.

This implementation supports conformance specification v1. Numeric operators recursively flatten lists and require finite numeric scalar inputs; their results may be nonfinite. Equality is structural for lists and objects and is cycle-safe.
