import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/auth/auth_state.dart';
import '../../../core/network/client.dart';
import '../../../core/network/failure.dart';
import '../data/api_payout_repository.dart';
import '../domain/payout_repository.dart';
import '../domain/payout_models.dart';
import '../../home/presentation/view_state.dart';

final payoutRepositoryProvider = Provider<PayoutRepository>(
  (ref) => ApiPayoutRepository(ref.watch(dioProvider)));

typedef PayoutKey = (String, String);
final payoutOrderProvider =
    FutureProvider.autoDispose.family<PayoutOrder, PayoutKey>(
  (ref, key) async {
    final person = ref.watch(fakeAuthProvider);
    if (person == null) throw const UnauthorisedFailure();
    return ref.watch(payoutRepositoryProvider).loadOrder(person, key.$1, key.$2);
  },
);

ViewState<PayoutOrder> payoutViewState(AsyncValue<PayoutOrder> async) =>
    switch (async) {
  AsyncData(:final value) => value.entries.isEmpty
      ? const ViewEmpty<PayoutOrder>() : ViewData<PayoutOrder>(value),
  AsyncError(:final error) => ViewFailure<PayoutOrder>(
      error is Failure ? error.message : 'Could not load payouts'),
  _ => const ViewLoading<PayoutOrder>(),
};
