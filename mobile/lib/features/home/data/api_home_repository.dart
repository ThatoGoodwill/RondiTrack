import 'package:dio/dio.dart';
import '../../../core/money/cents.dart';
import '../../../core/network/data/wire_models.dart';
import '../../../core/network/failure.dart';
import '../../../core/network/failure_mapper.dart';
import '../../stokvel/domain/statuses.dart';
import '../domain/home_repository.dart';
import '../domain/models.dart';

/// All wire-shape knowledge lives in data/. No DTO escapes to presentation.
class ApiHomeRepository implements HomeRepository {
  const ApiHomeRepository(this.dio);
  final Dio dio;

  @override
  Future<List<HomeEntry>> loadHome(String persona) async {
    try {
      final response = await dio.get<dynamic>('/api/v1/me/home');
      final wire = HomeWireResponse.fromJson(
          Map<String, dynamic>.from(response.data as Map));
      return wire.stokvels.map(mapHomeWire).toList(growable: false);
    } catch (error) {
      throw mapFailure(error);
    }
  }

  @override
  Future<StokvelDetails> loadDetail(String persona, String stokvelId) async {
    try {
      // The server-derived membership list is the authority. A non-member
      // sees the exact same Not found as an unknown id.
      final entries = await loadHome(persona);
      HomeEntry? selected;
      for (final entry in entries) {
        if (entry.membership.stokvelId == stokvelId) selected = entry;
      }
      if (selected == null) throw const NotFoundFailure();

      final cycle = selected.currentCycle;
      if (cycle == null) return StokvelDetails(home: selected, contributions: []);
      final response = await dio.get<dynamic>(
        '/api/v1/stokvels/${Uri.encodeComponent(stokvelId)}/contributions',
        queryParameters: {'cycleId': cycle.id, 'pageSize': 100},
      );
      final wire = ContributionPageWire.fromJson(
          Map<String, dynamic>.from(response.data as Map));
      final contributions = wire.items.map(mapContributionWire).toList(growable: false);
      return StokvelDetails(home: selected, contributions: contributions);
    } catch (error) {
      throw mapFailure(error);
    }
  }
}

HomeEntry mapHomeEntry(Map<String, dynamic> data) =>
    mapHomeWire(HomeStokvelWire.fromJson(data));

HomeEntry mapHomeWire(HomeStokvelWire data) {
  final raw = data.currentCycle;
  final cycle = raw == null ? null : CurrentCycle(
    id: raw.id,
    label: raw.label,
    status: CycleStatus.fromApi(raw.status),
    paidCents: Cents(raw.paidCents),
    owedCents: Cents(raw.owedCents),
    endsOnDate: raw.endsOn,
  );
  final due = data.nextPayoutDueUtc;
  return HomeEntry(
    membership: Membership(
      stokvelId: data.stokvelId,
      role: StokvelRole.fromApi(data.role)),
    name: data.name,
    arrearsCents: Cents(data.arrearsCents),
    currentCycle: cycle,
    nextPayoutDueUtc: due == null ? null : DateTime.parse(due).toUtc(),
  );
}

ContributionEntry mapContributionWire(ContributionWire data) {
  final rands = data.amount;
  final cents = (rands * 100).round();
  if ((rands * 100 - cents).abs() > 0.000001) {
    throw const UnexpectedFailure();
  }
  return ContributionEntry(
    id: data.id,
    userId: data.userId,
    amountCents: Cents(cents),
    recordedAtUtc: DateTime.parse(data.recordedAt).toUtc(),
    status: ContributionStatus.recorded,
  );
}
