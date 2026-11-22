import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/auth/auth_state.dart';
import '../../../core/network/client.dart';
import '../../../core/network/session.dart';
import '../../../core/network/failure.dart';
import '../data/api_home_repository.dart';
import '../domain/home_repository.dart';
import '../domain/models.dart';
import '../../activity/data/api_activity_repository.dart';

/// One constructor swap: Week 10 tests can override this with FakeHomeRepository.
final homeRepositoryProvider = Provider<HomeRepository>(
  (ref) => ApiHomeRepository(ref.watch(dioProvider)),
);

/// Retained to avoid breaking Week 10 test setup. Real auth is in memory.
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
    final persona = ref.watch(fakeAuthProvider);
    if (persona == null) throw const UnauthorisedFailure();
    return ref.watch(homeRepositoryProvider).loadHome(persona);
  }

  Future<void> reload() async {
    ref.invalidateSelf();
    await future;
  }
}
final memberHomeProvider =
    AsyncNotifierProvider.autoDispose<MemberHome, List<HomeEntry>>(
  MemberHome.new,
);

final stokvelDetailsProvider =
    FutureProvider.autoDispose.family<StokvelDetails, String>(
  (ref, id) async {
    final persona = ref.watch(fakeAuthProvider);
    if (persona == null) throw const UnauthorisedFailure();
    return ref.watch(homeRepositoryProvider).loadDetail(persona, id);
  },
);

final activityFeedProvider = FutureProvider.autoDispose<List<String>>((ref) async {
  final persona = ref.watch(fakeAuthProvider);
  if (persona == null) throw const UnauthorisedFailure();
  // A single provider override can supply fake home + fake activity to Week 10
  // tests; real activity is fetched from the member-scoped API.
  if (ref.read(accessSessionProvider) == null) {
    final home = await ref.watch(memberHomeProvider.future);
    return home.map((e) => '${e.name}: ${e.currentCycle?.status.label ?? "No active cycle"}').toList();
  }
  return ApiActivityRepository(ref.watch(dioProvider)).firstPage();
});
