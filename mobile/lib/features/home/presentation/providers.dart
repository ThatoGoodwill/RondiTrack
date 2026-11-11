import 'package:riverpod_annotation/riverpod_annotation.dart';
import '../data/fake_home_repository.dart';
import '../domain/home_repository.dart';
import '../domain/models.dart';

part 'providers.g.dart';

// The ONLY line Week 11 will change when a real API repository is ready.
@Riverpod(keepAlive: true)
HomeRepository homeRepository(Ref ref) => const FakeHomeRepository();

@Riverpod(keepAlive: true)
class SelectedPersona extends _$SelectedPersona {
  @override
  String build() => 'lerato';
  void select(String persona) => state = persona;
}

@riverpod
class MemberHome extends _$MemberHome {
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

// A family id is part of provider identity. Switching ids cannot display stale
// contributions from the previous stokvel for even a frame.
@riverpod
Future<StokvelDetails> stokvelDetails(Ref ref, String id) async {
  final persona = ref.watch(selectedPersonaProvider);
  return ref.watch(homeRepositoryProvider).loadDetail(persona, id);
}
