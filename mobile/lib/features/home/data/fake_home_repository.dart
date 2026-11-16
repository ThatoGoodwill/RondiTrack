import '../domain/not_found.dart';
import '../../../core/money/cents.dart';
import '../../stokvel/domain/statuses.dart';
import '../domain/home_repository.dart';
import '../domain/models.dart';

class FakeHomeRepository implements HomeRepository {
  const FakeHomeRepository({this.delay = const Duration(milliseconds: 150)});

  final Duration delay;

  Future<void> _latency() async {
    if (delay != Duration.zero) await Future<void>.delayed(delay);
  }

  static final ubuntu = HomeEntry(
    membership: const Membership(stokvelId: 'ubuntu', role: StokvelRole.member),
    name: 'Ubuntu Savers', arrearsCents: const Cents(49990),
    currentCycle: const CurrentCycle(
      id: 'ubuntu-cycle', label: '2026-11', status: CycleStatus.open,
      paidCents: Cents(10), owedCents: Cents(49990), endsOnDate: '2026-11-30',
    ),
    nextPayoutDueUtc: DateTime.utc(2026, 11, 30, 22),
  );
  static final mabena = HomeEntry(
    membership: const Membership(stokvelId: 'mabena', role: StokvelRole.treasurer),
    name: 'Mabena Family', arrearsCents: const Cents(0),
    currentCycle: const CurrentCycle(
      id: 'mabena-cycle', label: '2026-11', status: CycleStatus.open,
      paidCents: Cents(35050), owedCents: Cents(0), endsOnDate: '2026-11-30',
    ),
  );
  @override
  Future<List<HomeEntry>> loadHome(String persona) async {
    await _latency();
    return switch (persona) {
      'lerato' => [ubuntu, mabena],
      'sipho' => [HomeEntry(
        membership: const Membership(stokvelId: 'ubuntu', role: StokvelRole.member),
        name: 'Ubuntu Savers', arrearsCents: const Cents(0),
        currentCycle: const CurrentCycle(
          id: 'ubuntu-cycle', label: '2026-11', status: CycleStatus.open,
          paidCents: Cents(50000), owedCents: Cents(0),
        ),
      )],
      'nomsa' => [],
      _ => [],
    };
  }

  @override
  Future<StokvelDetails> loadDetail(String persona, String stokvelId) async {
    final home = await loadHome(persona);
    final matches = home.where((e) => e.membership.stokvelId == stokvelId);
    if (matches.isEmpty) throw const StokvelNotFound();
    return StokvelDetails(
      home: matches.first,
      contributions: [
        ContributionEntry(
          id: 'demo-' + stokvelId,
          userId: persona, amountCents: const Cents(15050),
          recordedAtUtc: DateTime.utc(2026, 11, 1),
          status: ContributionStatus.recorded,
        ),
      ],
    );
  }
}
