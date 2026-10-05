# Conformance

Cross-language compatibility is specified and tested by the independent [simple-expressions-conformance](https://github.com/LieselThuriot/simple-expressions-conformance) repository.

The pinned `conformance/` submodule supplies `spec/v1/cases.json`, which is executed by `ConformanceTests`. Initialize it with `git submodule update --init --recursive`. The test reads the corpus through the submodule-linked test asset and does not copy files into the implementation repository.

The conformance suite is the minimum shared test set. The additional local tests cover CLR model access, caching, and port-specific semantics.

This implementation supports conformance specification v1 and the portable semantics covered by local tests. It retains JavaScript-style truthiness. Numeric operators accept only finite numbers and finite signed decimal/exponent strings, recursively flatten variadic lists before conversion, and require finite results; division by zero has a specific error. `round` accepts precision 0 through 308 and rounds ties away from zero. Scalar negative zero is normalized, while nested values in live model containers are not copied. Equality and comparison use explicit primitive, numeric-string, and collection-shape rules; structural list/object equality is cycle-safe. Expression trimming, token separation, `trim`, and numeric conversion share the declared Unicode White_Space set excluding `U+FEFF`, and cyclic list text conversion throws `Cyclic list value`. Boolean cache keys use lowercase strings and share entries with `"true"` and `"false"`.
