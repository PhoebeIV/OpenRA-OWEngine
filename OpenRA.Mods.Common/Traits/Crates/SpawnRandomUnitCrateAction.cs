#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Spawns units when collected.")]
	class SpawnRandomUnitCrateActionInfo : CrateActionInfo
	{
		[ActorReference]
		[FieldLoader.Require]
		[Desc("The list of units to spawn.")]
		public readonly ImmutableArray<string> Units = [];

		[Desc("Factions that are allowed to trigger this action.")]
		public readonly FrozenSet<string> ValidFactions = FrozenSet<string>.Empty;

		[Desc("Override the owner of the newly spawned unit: e.g. Creeps or Neutral")]
		public readonly string Owner = null;

		[Desc("Ignores the spawned actor's locomotor when spawning the unit.")]
		public readonly bool IgnoreLocomotor = false;

		public override object Create(ActorInitializer init) { return new SpawnRandomUnitCrateAction(init.Self, this); }
	}

	class SpawnRandomUnitCrateAction : CrateAction
	{
		readonly Actor self;
		readonly SpawnRandomUnitCrateActionInfo info;

		public SpawnRandomUnitCrateAction(Actor self, SpawnRandomUnitCrateActionInfo info)
			: base(self, info)
		{
			this.self = self;
			this.info = info;
			if (info.Units.Length == 0)
				throw new YamlException(
					"A SpawnRandomUnitCrateAction does not specify any units to give. " +
					"This might be because the yaml is referring to 'Unit' rather than 'Units'.");
		}

		public bool CanGiveTo(Actor collector)
		{
			if (collector.Owner.NonCombatant)
				return false;

			if (info.ValidFactions.Count > 0 && !info.ValidFactions.Contains(collector.Owner.Faction.InternalName))
				return false;

			return true;
		}

		public override int GetSelectionShares(Actor collector)
		{
			if (!CanGiveTo(collector))
				return 0;

			return base.GetSelectionShares(collector);
		}

		public override void Activate(Actor collector)
		{
			collector.World.AddFrameEndTask(w =>
			{
				var pathFinder = w.WorldActor.TraitOrDefault<IPathFinder>();
				var locomotorsByName = w.WorldActor.TraitsImplementing<Locomotor>().ToDictionary(l => l.Info.Name);
				var randomLocation = collector.World.Map.ChooseRandomCell(collector.World.SharedRandom);

				if (!info.IgnoreLocomotor)
				{
					foreach (var unit in info.Units)
					{
						var location = ChooseEmptyCellNear(collector, unit, pathFinder, locomotorsByName);
						if (location != null)
						{
							var actor = w.CreateActor(unit,
							[
								new LocationInit(location.Value),
								new OwnerInit(info.Owner ?? collector.Owner.InternalName)
							]);

							// Set the subcell and make sure to crush actors beneath.
							var positionable = actor.OccupiesSpace as IPositionable;
							positionable.SetPosition(actor, location.Value, positionable.GetAvailableSubCell(location.Value, ignoreActor: actor));
						}
					}
				}
				else
				{
					foreach (var unit in info.Units)
					{
						var actor = w.CreateActor(unit,
						[
							new LocationInit(randomLocation),
							new OwnerInit(info.Owner ?? collector.Owner.InternalName)
						]);

						// Set the subcell and make sure to crush actors beneath.
						var positionable = actor.OccupiesSpace as IPositionable;
						positionable.SetPosition(actor, randomLocation, positionable.GetAvailableSubCell(randomLocation, ignoreActor: actor));
					}
				}
			});

			base.Activate(collector);
		}

		IEnumerable<CPos> GetSuitableCells(CPos near, string unitName, IPathFinder pathFinder, Dictionary<string, Locomotor> locomotorsByName)
		{
			var actorRules = self.World.Map.Rules.Actors[unitName];

			Locomotor locomotor = null;
			if (pathFinder != null)
			{
				var locomotorName = actorRules.TraitInfoOrDefault<MobileInfo>()?.Locomotor;
				locomotor = locomotorName != null ? locomotorsByName[locomotorName] : null;
			}

			var ip = actorRules.TraitInfo<IPositionableInfo>();
			for (var i = -1; i <= 1; i++)
			{
				for (var j = -1; j <= 1; j++)
				{
					var cell = near + new CVec(i, j);
					if (ip.CanEnterCell(self.World, self, cell) &&
						(locomotor == null || pathFinder.PathMightExistForLocomotorBlockedByImmovable(locomotor, cell, near)))
						yield return near + new CVec(i, j);
				}
			}
		}

		CPos? ChooseEmptyCellNear(Actor a, string unit, IPathFinder pathFinder, Dictionary<string, Locomotor> locomotorsByName)
		{
			return GetSuitableCells(a.World.Map.ChooseRandomCell(a.World.SharedRandom), unit, pathFinder, locomotorsByName)
				.Cast<CPos?>()
				.RandomOrDefault(self.World.SharedRandom);
		}
	}
}
