import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/routing/routes.dart';
import '../../../core/money/cents.dart';
import '../../home/presentation/providers.dart';
import '../../home/domain/not_found.dart';

class CycleScreen extends ConsumerWidget {
  const CycleScreen({required this.stokvelId, required this.cycleId, super.key});
  final String stokvelId;
  final String cycleId;
  @override Widget build(BuildContext context, WidgetRef ref) {
    final detail = ref.watch(stokvelDetailsProvider(stokvelId));
    return Scaffold(appBar: AppBar(title: const Text('Cycle'), leading: BackButton(
      onPressed: () => StokvelDestination(stokvelId).go(context))),
      body: detail.when(
        loading: () => const Center(child: CircularProgressIndicator()),
        error: (error, _) => Center(child: Text(error is StokvelNotFound ? 'Not found' : 'Could not load cycle')),
        data: (data) {
          final cycle = data.home.currentCycle;
          if (cycle == null || cycle.id != cycleId) return const Center(child: Text('Not found'));
          return ListView(children: [
            ListTile(title: Text(cycle.label), subtitle: Text(cycle.status.label)),
            ListTile(title: const Text('Paid'), subtitle: Text(cycle.paidCents.formatZa())),
            ListTile(title: const Text('Owed'), subtitle: Text(cycle.owedCents.formatZa())),
            ListTile(title: const Text('Rotation and payouts'),
              onTap: () => PayoutDestination(stokvelId, cycleId).go(context)),
          ]);
        }));
  }
}
