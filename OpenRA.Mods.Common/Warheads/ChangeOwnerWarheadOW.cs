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

using System.Linq;
using OpenRA.GameRules;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Warheads
{
	public enum NewOwnerType { Attacker, InternalName }

	[Desc("Interacts with the `" + nameof(TemporaryOwnerManager) + "` trait. This version allows you to specify an owner ie. Creeps or Neutral.")]
	public class ChangeOwnerOWWarhead : Warhead
	{
		[Desc("Duration of the owner change (in ticks). Set to 0 to make it permanent.")]
		public readonly int Duration = 0;

		public readonly NewOwnerType OwnerType = NewOwnerType.Attacker;

		public readonly string InternalName = "Neutral";

		public readonly WDist Range = WDist.FromCells(1);

		public override void DoImpact(in Target target, WarheadArgs args)
		{
			var firedBy = args.SourceActor;
			var actors = target.Type == TargetType.Actor ? [target.Actor] :
				firedBy.World.FindActorsInCircle(target.CenterPosition, Range);

			foreach (var a in actors)
			{
				if (!IsValidAgainst(a, firedBy))
					continue;

				// Don't do anything on friendly fire
				if (a.Owner == firedBy.Owner && OwnerType == NewOwnerType.Attacker)
					continue;

				if (Duration == 0 && OwnerType == NewOwnerType.Attacker)
					a.ChangeOwner(firedBy.Owner); // Permanent
				else if (Duration == 0 && OwnerType == NewOwnerType.InternalName)
					a.ChangeOwner(firedBy.World.Players.First(p => p.InternalName == InternalName));
				else
				{
					var tempOwnerManager = a.TraitOrDefault<TemporaryOwnerManager>();
					if (tempOwnerManager == null)
						continue;

					if (OwnerType == NewOwnerType.Attacker)
						tempOwnerManager.ChangeOwner(a, firedBy.Owner, Duration);
					else
						tempOwnerManager.ChangeOwner(a, firedBy.World.Players.First(p => p.InternalName == InternalName), Duration);
				}

				// Stop shooting, you have new enemies
				a.CancelActivity();
			}
		}
	}
}
