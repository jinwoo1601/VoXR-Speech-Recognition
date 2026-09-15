// ============================================================================
// Purpose:  Holds the two runtime slot registries: value providers that filter which values the
//           parser accepts, and resolvers that fill a required slot the speaker omitted
// Layer:    Runtime.Commands
// Owns:     DynamicSlotManager (internal sealed class: the provider and resolver registries)
// Depends:  VoxrSlotDefinition, VoxrSlotType, VoxrSlotResolution
// ============================================================================
using System;
using System.Collections.Generic;

namespace VoXR.Commands
{
    internal sealed class DynamicSlotManager
    {
        Dictionary<string, Func<string[]>> _providers;

        internal bool HasProviders => _providers != null && _providers.Count > 0;

        internal void Register(string slotName, Func<string[]> provider)
        {
            if (slotName == null) throw new ArgumentNullException(nameof(slotName));
            if (provider == null) throw new ArgumentNullException(nameof(provider));

            if (_providers == null)
                _providers = new Dictionary<string, Func<string[]>>(StringComparer.Ordinal);

            _providers[slotName] = provider;
        }

        internal bool Unregister(string slotName)
        {
            if (slotName == null) throw new ArgumentNullException(nameof(slotName));
            return _providers != null && _providers.Remove(slotName);
        }

        internal VoxrSlotDefinition[] BuildEffectiveSlots(VoxrSlotDefinition[] baseSlots)
        {
            if (_providers == null || _providers.Count == 0)
                return baseSlots;

            VoxrSlotDefinition[] effective = null;

            for (int i = 0; i < baseSlots.Length; i++)
            {
                var slot = baseSlots[i];

                if (slot.Type == VoxrSlotType.NumberSequence ||
                    !_providers.TryGetValue(slot.Name, out var provider))
                {
                    if (effective != null)
                        effective[i] = slot;
                    continue;
                }

                var activeValues = provider();
                if (activeValues == null)
                {
                    if (effective != null)
                        effective[i] = slot;
                    continue;
                }

                if (effective == null)
                {
                    effective = new VoxrSlotDefinition[baseSlots.Length];
                    Array.Copy(baseSlots, effective, i);
                }

                if (activeValues.Length == 0)
                {
                    effective[i] = new VoxrSlotDefinition(slot.Name, Array.Empty<string>(), null);
                    continue;
                }

                var activeSet = new HashSet<string>(activeValues, StringComparer.Ordinal);

                Dictionary<string, string> filteredAliases = null;
                if (slot.Aliases != null)
                {
                    foreach (var kvp in slot.Aliases)
                    {
                        if (activeSet.Contains(kvp.Value))
                        {
                            if (filteredAliases == null)
                                filteredAliases = new Dictionary<string, string>(StringComparer.Ordinal);
                            filteredAliases[kvp.Key] = kvp.Value;
                        }
                    }
                }

                effective[i] = new VoxrSlotDefinition(slot.Name, activeValues, filteredAliases);
            }

            return effective ?? baseSlots;
        }

        // Resolvers are a second registry rather than entries in _providers because the two answer
        // different questions at different moments: a provider narrows what the parser will MATCH
        // and takes effect only on a parser rebuild, while a resolver fills what the parser did
        // NOT match and needs no rebuild at all. Merging them would tie a resolver's registration
        // to a grammar rebuild it has no reason to trigger.
        Dictionary<string, Func<VoxrSlotResolution>> _resolvers;

        internal bool HasResolvers => _resolvers != null && _resolvers.Count > 0;

        internal void RegisterResolver(string slotName, Func<VoxrSlotResolution> resolver)
        {
            if (slotName == null) throw new ArgumentNullException(nameof(slotName));
            if (resolver == null) throw new ArgumentNullException(nameof(resolver));

            if (_resolvers == null)
                _resolvers = new Dictionary<string, Func<VoxrSlotResolution>>(StringComparer.Ordinal);

            _resolvers[slotName] = resolver;
        }

        internal bool UnregisterResolver(string slotName)
        {
            if (slotName == null) throw new ArgumentNullException(nameof(slotName));
            return _resolvers != null && _resolvers.Remove(slotName);
        }

        internal bool TryGetResolver(string slotName, out Func<VoxrSlotResolution> resolver)
        {
            if (_resolvers == null)
            {
                resolver = null;
                return false;
            }

            return _resolvers.TryGetValue(slotName, out resolver);
        }
    }
}
