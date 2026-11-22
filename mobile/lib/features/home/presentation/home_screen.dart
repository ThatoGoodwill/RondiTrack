import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/money/cents.dart';
import '../../../core/widgets/failure_message.dart';
import '../../../core/routing/routes.dart';
import '../domain/models.dart';
import 'providers.dart';

class HomeScreen extends ConsumerWidget {
  const HomeScreen({super.key});
  @override Widget build(BuildContext context, WidgetRef ref) {
    final home = ref.watch(memberHomeProvider);
    return Scaffold(appBar: AppBar(title: const Text('RondiTrack')),
      body: home.when(
        loading: () => const Center(child: CircularProgressIndicator(key: Key('home-loading'))),
        error: (e, _) => FailureMessage(error: e,
          retry: () => ref.invalidate(memberHomeProvider)),
        data: (entries) => entries.isEmpty
          ? const Center(child: Padding(padding: EdgeInsets.all(20),
            child: Text('You do not belong to any stokvels yet. Ask for an invite.')))
          : RefreshIndicator(onRefresh: () => ref.refresh(memberHomeProvider.future).then<void>((_) {}),
            child: ListView.builder(key: const Key('home-list'), itemCount: entries.length,
              itemBuilder: (context, i) => _card(context, entries[i])))));
  }
  Widget _card(BuildContext context, HomeEntry entry) {
    final cycle = entry.currentCycle;
    return Card(child: ListTile(
      title: Text(entry.name),
      subtitle: Text('${entry.membership.role.label}\n'
        'Paid: ${cycle?.paidCents.formatZa() ?? '—'} · Owed: ${cycle?.owedCents.formatZa() ?? '—'}\n'
        'Arrears: ${entry.arrearsCents.formatZa()}\n'
        'Next payout: ${entry.nextPayoutDueUtc?.toIso8601String() ?? 'Not scheduled'}'),
      isThreeLine: true, trailing: const Icon(Icons.chevron_right),
      onTap: () => StokvelDestination(entry.membership.stokvelId).go(context)));
  }
}
