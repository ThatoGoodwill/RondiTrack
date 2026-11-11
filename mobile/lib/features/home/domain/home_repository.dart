import 'models.dart';

/// Swapping FakeHomeRepository for an HTTP implementation in Week 11 happens
/// behind this interface. Presentation code never imports data implementations.
abstract interface class HomeRepository {
  /// RondiTrack Future declaration.
  Future<List<HomeEntry>> loadHome(String persona);
  /// RondiTrack Future declaration.
  Future<StokvelDetails> loadDetail(String persona, String stokvelId);
}
