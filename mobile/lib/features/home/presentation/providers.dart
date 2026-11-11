import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../data/fake_home_repository.dart';
import '../domain/home_repository.dart';
import '../domain/models.dart';

// Riverpod's state lifetime is explicit. Freezed continues to generate model code.
// Explicit providers avoid the unresolved-type failure in generator 4.0.9.
final homeRepositoryProvider = Provider<HomeRepository>(
  (ref) => const FakeHomeRepository(),
);

class SelectedPersona extends Notifier<String> {
  @override
  String build() => 'lerato';
  void select(String value) => state = value;
}
final selectedPersonaProvider = NotifierProvider<SelectedPersona, String>(
  SelectedPersona.new,
);

class MemberHome extends AsyncNotifier<List<HomeEntry>> {
  @override
  Future<List<HomeEntry>> build() async {
    final persona = ref.watch(selectedPersonaProvider);
    return ref.watch(homeRepositoryProvider).loadHome(persona);
  }
  Future<void> reload() async {
    ref.invalidateSelf();
    await future;
  }
}
final memberHomeProvider = AsyncNotifierProvider.autoDispose<MemberHome, List<HomeEntry>>(
  MemberHome.new,
);

final stokvelDetailsProvider = FutureProvider.autoDispose.family<StokvelDetails, String>(
  (ref, id) async {
    final persona = ref.watch(selectedPersonaProvider);
    return ref.watch(homeRepositoryProvider).loadDetail(persona, id);
  },
);
