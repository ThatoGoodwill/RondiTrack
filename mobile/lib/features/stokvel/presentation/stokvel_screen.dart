import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/money/cents.dart';
import '../../../core/layout/breakpoints.dart';
import 'stokvel_list_screen.dart';
import '../../../core/routing/routes.dart';
import '../../home/domain/not_found.dart';
import '../../home/presentation/providers.dart';
import '../domain/actions.dart';
import '../domain/statuses.dart';

class StokvelScreen extends StatelessWidget {
  const StokvelScreen({required this.initialId, super.key});
  final String initialId;
  @override Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('Stokvel detail')),
    body: LayoutBuilder(builder: (context, constraints) =>
      windowClass(constraints.maxWidth) == WindowClass.expanded
        ? Row(children: [
            SizedBox(width: 300, child: StokvelSelectionList(selectedId: initialId)),
            const VerticalDivider(width: 1),
            Expanded(child: StokvelDetailsBody(stokvelId: initialId)),
          ])
        : StokvelDetailsBody(stokvelId: initialId)));
}

class StokvelDetailsBody extends ConsumerWidget {
  const StokvelDetailsBody({required this.stokvelId, super.key});
  final String stokvelId;
  @override Widget build(BuildContext context, WidgetRef ref) {
    final details = ref.watch(stokvelDetailsProvider(stokvelId));
    return details.when(
      loading: () => const Center(child: CircularProgressIndicator(key: Key('detail-loading'))),
      error: (e, _) => Center(child: e is StokvelNotFound
        ? const Text('Not found')
        : Column(mainAxisSize: MainAxisSize.min, children: [
            const Text('Could not load this stokvel'),
            TextButton(onPressed: () => ref.invalidate(stokvelDetailsProvider(stokvelId)),
              child: const Text('Try again'))])),
      data: (detail) {
        final entry = detail.home;
        final cycle = entry.currentCycle;
        final actions = permittedActions((entry.membership.role,
          cycle?.status ?? CycleStatus.unknown,
          detail.contributions.isEmpty ? ContributionStatus.notRecorded : ContributionStatus.recorded));
        return ListView(children: [
          ListTile(title: Text(entry.name),
            subtitle: Text('${entry.membership.role.label} · ${cycle?.status.label ?? 'No cycle'}')),
          ListTile(title: const Text('Paid this cycle'), subtitle: Text(cycle?.paidCents.formatZa() ?? '—')),
          ListTile(title: const Text('Still owed'), subtitle: Text(cycle?.owedCents.formatZa() ?? '—')),
          for (final item in detail.contributions)
            ListTile(title: const Text('Contribution'), subtitle: Text(item.amountCents.formatZa())),
          if (cycle != null) ListTile(
            title: const Text('View cycle'), trailing: const Icon(Icons.chevron_right),
            onTap: () => CycleDestination(stokvelId, cycle.id).go(context)),
          for (final action in actions)
            ListTile(title: Text(switch (action) {
              StokvelAction.pay => 'Record contribution (Week 11)',
              StokvelAction.uploadProof => 'Upload proof (Week 11)',
              StokvelAction.confirmContribution => 'Confirm contributions (Week 11)',
              StokvelAction.closeCycle => 'Close cycle (Week 11)',
              StokvelAction.recordPayout => 'Record payout (Week 11)',
            })),
        ]);
      });
  }
}
