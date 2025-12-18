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

using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Effects;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Spawns an actor that stays for a limited amount of time. This one allows you to specify whether it destroys or removes the actor, and offset option")]
	public class SpawnActorPowerOWInfo : SpawnActorPowerInfo
	{
		[ActorReference]
		[Desc("Offset of the spawned actor relative to the target position.",
			"Warning: Spawning an actor outside the target cell might",
			"lead to unexpected behaviour.")]
		public readonly CVec Offset = CVec.Zero;

		public override object Create(ActorInitializer init) { return new SpawnActorPowerOW(init.Self, this); }
	}

	public class SpawnActorPowerOW : SpawnActorPower
	{
		public SpawnActorPowerOW(Actor self, SpawnActorPowerOWInfo info)
			: base(self, info) { }

		public override void Activate(Actor self, Order order, SupportPowerManager manager)
		{
			var info = Info as SpawnActorPowerOWInfo;
			var position = order.Target.CenterPosition;
			var cell = self.World.Map.CellContaining(position);

			if (!Validate(self.World, info, cell))
				return;

			base.Activate(self, order, manager);

			self.World.AddFrameEndTask(w =>
			{
				PlayLaunchSounds();
				Game.Sound.Play(SoundType.World, info.DeploySound, position);

				if (!string.IsNullOrEmpty(info.EffectSequence) && !string.IsNullOrEmpty(info.EffectPalette))
				{
					var palette = info.EffectPalette;
					if (info.EffectPaletteIsPlayerPalette)
						palette += self.Owner.InternalName;

					w.Add(new SpriteEffect(position, w, info.EffectImage, info.EffectSequence, palette));
				}

				var actor = w.CreateActor(info.Actor,
				[
					new LocationInit(cell + info.Offset),
					new OwnerInit(self.Owner),
				]);

				if (info.LifeTime > -1)
				{
					actor.QueueActivity(new Wait(info.LifeTime));
					actor.QueueActivity(new RemoveSelf());
				}
			});
		}

		public bool Validate(World world, SpawnActorPowerOWInfo info, CPos cell)
		{
			if (!world.Map.Contains(cell) || !world.Map.Contains(cell + info.Offset))
				return false;

			if (!info.AllowUnderShroud && world.ShroudObscures(cell))
				return false;

			if (info.Terrain != null && !info.Terrain.Contains(world.Map.GetTerrainInfo(cell).Type))
				return false;

			return true;
		}
	}
}
