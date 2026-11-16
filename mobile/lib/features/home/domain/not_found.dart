/// Share one not-found result with the API's membership-aware 404 semantics.
final class StokvelNotFound implements Exception {
  const StokvelNotFound();
  @override String toString() => 'Not found';
}
