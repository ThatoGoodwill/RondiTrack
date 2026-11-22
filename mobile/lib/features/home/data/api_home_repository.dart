import 'package:dio/dio.dart';
import '../../../core/money/cents.dart';
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
      final body = Map<String, dynamic>.from(response.data as Map);
      final rows = body['stokvels'] as List;
      return rows.map((value) => mapHomeEntry(Map<String, dynamic>.from(value as Map)))
          .toList(growable: false);
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
      final json = Map<String, dynamic>.from(response.data as Map);
      final contributions = (json['items'] as List)
          .map((value) => mapContribution(Map<String, dynamic>.from(value as Map)))
          .toList(growable: false);
      return StokvelDetails(home: selected, contributions: contributions);
    } catch (error) {
      throw mapFailure(error);
    }
  }
}

HomeEntry mapHomeEntry(Map<String, dynamic> data) {
  final cycleData = data['currentCycle'];
  CurrentCycle? cycle;
  if (cycleData != null) {
    final raw = Map<String, dynamic>.from(cycleData as Map);
    cycle = CurrentCycle(
      id: raw['id'] as String,
      label: raw['label'] as String,
      status: CycleStatus.fromApi(raw['status'] as String),
      paidCents: Cents((raw['paidCents'] as num).toInt()),
      owedCents: Cents((raw['owedCents'] as num).toInt()),
      endsOnDate: raw['endsOn'] as String?,
    );
  }
  final due = data['nextPayoutDueUtc'] as String?;
  return HomeEntry(
    membership: Membership(
      stokvelId: data['stokvelId'] as String,
      role: StokvelRole.fromApi(data['role'] as String)),
    name: data['name'] as String,
    arrearsCents: Cents((data['arrearsCents'] as num).toInt()),
    currentCycle: cycle,
    nextPayoutDueUtc: due == null ? null : DateTime.parse(due).toUtc(),
  );
}

/// ContributionResponse's amount is decimal *rands*, not integer cents.
ContributionEntry mapContribution(Map<String, dynamic> data) {
  final rands = data['amount'] as num;
  final cents = (rands * 100).round();
  if ((rands * 100 - cents).abs() > 0.000001) {
    throw const UnexpectedFailure();
  }
  return ContributionEntry(
    id: data['id'] as String,
    userId: data['userId'] as String,
    amountCents: Cents(cents),
    recordedAtUtc: DateTime.parse(data['recordedAt'] as String).toUtc(),
    status: ContributionStatus.recorded,
  );
}
