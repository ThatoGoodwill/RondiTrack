import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/money/cents.dart';
import '../../../core/routing/routes.dart';
import '../../../core/theme/rondi_theme.dart';
import '../../home/presentation/providers.dart';
import '../../home/domain/not_found.dart';
import '../../home/presentation/view_state.dart';
import '../../stokvel/domain/statuses.dart';
import '../domain/payout_models.dart';
import 'providers.dart';

class PayoutScreen extends ConsumerWidget {
  const PayoutScreen({required this.stokvelId, required this.cycleId, super.key});
  final String stokvelId, cycleId;
  @override Widget build(BuildContext context, WidgetRef ref) {
    final details = ref.watch(stokvelDetailsProvider(stokvelId));
    final payout = ref.watch(payoutOrderProvider((stokvelId, cycleId)));
    return Scaffold(appBar: AppBar(title: const Text('Rotation and payouts'),
      leading: BackButton(onPressed: () => CycleDestination(stokvelId, cycleId).go(context))),
      body: details.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (e, _) => Center(child: Text(e is StokvelNotFound ? 'Not found' : 'Could not load stokvel')),
        data: (detail) {
          if (detail.home.currentCycle?.id != cycleId) return const Center(child: Text('Not found'));
          return switch (payoutViewState(payout)) {
            ViewLoading() => const Center(child: CircularProgressIndicator()),
            ViewEmpty() => const Center(child: Text('No payout order has been set for this cycle yet.')),
            ViewFailure(:final message) => Center(child: Column(mainAxisSize: MainAxisSize.min, children: [
              Text(message),
              if (message != 'Not found') TextButton(
                onPressed: () => ref.invalidate(payoutOrderProvider((stokvelId, cycleId))),
                child: const Text('Try again'))])),
            ViewData(:final value) => ListView(children: [
              for (final e in value.entries) _PayoutTile(entry: e,
                isNext: value.nextMemberId == e.memberId),
              if (detail.home.membership.role == StokvelRole.treasurer)
                const ListTile(title: Text('Record payout (API connection pending)'),
                  subtitle: Text('Treasurer-only action; not available with Week 10 fakes')),
            ]),
          };
        }));
  }
}

class _PayoutTile extends StatelessWidget {
  const _PayoutTile({required this.entry, required this.isNext});
  final PayoutOrderEntry entry;
  final bool isNext;
  @override Widget build(BuildContext context) {
    final tokens = Theme.of(context).extension<RondiStatusColors>()!;
    final color = switch (entry.status) {
      PayoutStatus.paid => tokens.payoutPaid,
      PayoutStatus.next => tokens.payoutNext,
      PayoutStatus.waiting => tokens.payoutWaiting,
      PayoutStatus.unknown => tokens.unknown,
    };
    return ListTile(isThreeLine: true,
      title: Text('${entry.position}. ${entry.memberName}'),
      subtitle: Wrap(spacing: 8, runSpacing: 4, children: [
        Text(entry.amountCents.formatZa()),
        Text(entry.status.label, style: TextStyle(color: color, fontWeight: FontWeight.bold)),
        if (isNext) const Text('NEXT RECIPIENT', style: TextStyle(fontWeight: FontWeight.bold)),
      ]));
  }
}
