import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import '../../../core/layout/breakpoints.dart';
import '../../../core/routing/routes.dart';
import '../../home/domain/models.dart';
import '../../home/presentation/providers.dart';
import 'stokvel_screen.dart';

class StokvelSelectionList extends ConsumerWidget {
  const StokvelSelectionList({this.selectedId, super.key});
  final String? selectedId;
  @override Widget build(BuildContext context, WidgetRef ref) {
    final entries = ref.watch(memberHomeProvider);
    return entries.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (e, _) => const Center(child: Text('Could not load stokvels')),
      data: (items) => ListView(children: [for (final item in items)
        ListTile(title: Text(item.name), selected: selectedId == item.membership.stokvelId,
          onTap: () => StokvelDestination(item.membership.stokvelId).go(context))]));
  }
}

class StokvelListScreen extends ConsumerWidget {
  const StokvelListScreen({this.selectedId, super.key});
  final String? selectedId;
  @override Widget build(BuildContext context, WidgetRef ref) {
    final entries = ref.watch(memberHomeProvider);
    Widget list = entries.when(
      loading: () => const Center(child: CircularProgressIndicator()),
      error: (e, _) => Center(child: TextButton(
        onPressed: () => ref.invalidate(memberHomeProvider), child: const Text('Try again'))),
      data: (items) => items.isEmpty
        ? const Center(child: Text('No stokvels yet. Ask a member for an invite.'))
        : ListView(children: [for (final HomeEntry e in items)
            ListTile(key: Key('stokvel-${e.membership.stokvelId}'),
              selected: selectedId == e.membership.stokvelId,
              title: Text(e.name), subtitle: Text(e.membership.role.label),
              onTap: () => StokvelDestination(e.membership.stokvelId).go(context))]));
    return Scaffold(appBar: AppBar(title: const Text('Stokvels')),
      body: LayoutBuilder(builder: (context, constraints) {
        if (windowClass(constraints.maxWidth) != WindowClass.expanded) return list;
        return Row(children: [
          SizedBox(width: 300, child: list),
          const VerticalDivider(width: 1),
          Expanded(child: selectedId == null
            ? const Center(child: Text('Select a stokvel to view details'))
            : StokvelDetailsBody(stokvelId: selectedId!)),
        ]);
      }));
  }
}
