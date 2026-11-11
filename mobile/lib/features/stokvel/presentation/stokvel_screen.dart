import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/money/cents.dart';
import '../../home/presentation/providers.dart';
import '../domain/actions.dart';
import '../domain/statuses.dart';

class StokvelScreen extends ConsumerStatefulWidget {
  const StokvelScreen({required this.initialId, super.key});
  final String initialId;

  @override
  ConsumerState<StokvelScreen> createState() => _StokvelScreenState();
}

class _StokvelScreenState extends ConsumerState<StokvelScreen> {
  late String selectedId;

  @override
  void initState() {
    super.initState();
    selectedId = widget.initialId;
  }

  @override
  Widget build(BuildContext context) {
    final home = ref.watch(memberHomeProvider);
    final ids = home.value?.map((entry) => entry.membership.stokvelId).toList() ?? [selectedId];
    final details = ref.watch(stokvelDetailsProvider(selectedId));
    return Scaffold(
      appBar: AppBar(
        title: const Text('Stokvel detail'),
        actions: [
          DropdownButton<String>(
            key: const Key('stokvel-picker'), value: selectedId,
            items: ids.map((id) => DropdownMenuItem<String>(value: id, child: Text(id))).toList(),
            onChanged: (next) { if (next != null) setState(() => selectedId = next); },
          ),
        ],
      ),
      body: details.when(
        loading: () => const Center(child: CircularProgressIndicator(key: Key('detail-loading'))),
        error: (error, stack) => Center(child: Column(mainAxisSize: MainAxisSize.min, children: [
          const Text('We could not find that stokvel.'),
          TextButton(onPressed: () => ref.invalidate(stokvelDetailsProvider(selectedId)),
            child: const Text('Try again')),
        ])),
        data: (detail) {
          final entry = detail.home;
          final cycle = entry.currentCycle;
          final actions = permittedActions((
            entry.membership.role, cycle?.status ?? CycleStatus.unknown,
            detail.contributions.isEmpty ? ContributionStatus.notRecorded : ContributionStatus.recorded,
          ));
          return ListView(children: [
            ListTile(title: Text(entry.name),
              subtitle: Text(entry.membership.role.label + ' · ' + (cycle?.status.label ?? 'No cycle'))),
            ListTile(title: const Text('Paid this cycle'),
              subtitle: Text(cycle?.paidCents.formatZa() ?? '—')),
            ListTile(title: const Text('Still owed'),
              subtitle: Text(cycle?.owedCents.formatZa() ?? '—')),
            for (final item in detail.contributions)
              ListTile(title: const Text('Contribution'), subtitle: Text(item.amountCents.formatZa())),
            for (final action in actions)
              ListTile(title: Text(switch (action) {
                StokvelAction.pay => 'Record contribution (Week 11)',
                StokvelAction.uploadProof => 'Upload proof (Week 11)',
                StokvelAction.confirmContribution => 'Confirm contributions (Week 11)',
                StokvelAction.closeCycle => 'Close cycle (Week 11)',
                StokvelAction.recordPayout => 'Record payout (Week 11)',
              })),
          ]);
        },
      ),
    );
  }
}
