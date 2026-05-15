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

using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Gives a supportpower to the collector.")]
	sealed class ExplodeSupportPowerCrateActionInfo : CrateActionInfo
	{
		[ActorReference]
		[FieldLoader.Require]
		[Desc("Which proxy actor, which grants the support power, to spawn.")]
		public readonly string Proxy = null;
		[WeaponReference]
		[FieldLoader.Require]
		[Desc("The weapon to fire upon collection.")]
		public readonly string Weapon = null;

		public override object Create(ActorInitializer init) { return new ExplodeSupportPowerCrateAction(init.Self, this); }
	}

	sealed class ExplodeSupportPowerCrateAction : CrateAction
	{
		readonly ExplodeSupportPowerCrateActionInfo info;
		public ExplodeSupportPowerCrateAction(Actor self, ExplodeSupportPowerCrateActionInfo info)
			: base(self, info)
		{
			this.info = info;
		}

		// The free unit crate requires same faction and the actor needs to be mobile.
		// We want neither of these properties for crate power proxies.
		public override void Activate(Actor collector)
		{
			var weapon = collector.World.Map.Rules.Weapons[info.Weapon.ToLowerInvariant()];
			weapon.Impact(Target.FromPos(collector.CenterPosition), collector);

			collector.World.AddFrameEndTask(w => w.CreateActor(info.Proxy,
			[
				new OwnerInit(collector.Owner)
			]));

			base.Activate(collector);
		}
	}
}
