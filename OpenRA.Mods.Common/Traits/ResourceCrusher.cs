#region Copyright & License Information
/*
 * Copyright 2015- OpenRA.Mods.AS Developers (see AUTHORS)
 * This file is a part of a third-party plugin for OpenRA, which is
 * free software. It is made available to you under the terms of the
 * GNU General Public License as published by the Free Software
 * Foundation. For more information, see COPYING.
 */
#endregion

using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Effects;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.AS.Traits
{
	[Desc("Allows the actor to crush and refine resources.")]
	public class ResourceCrusherInfo : ConditionalTraitInfo
	{
		[Desc("The resource this actor can crush.")]
		public readonly string ResourceType;

		[Desc("Percentage of the resource value earned.")]
		public int ValueModifier = 100;

		public readonly bool ShowTicks = true;
		public readonly int TickLifetime = 30;

		[Desc("Notify other actors (purifiers etc) on successful crushing.")]
		public bool NotifyOtherActors = false;

		public override object Create(ActorInitializer init) { return new ResourceCrusher(init.Self, this); }
	}

	public class ResourceCrusher : ConditionalTrait<ResourceCrusherInfo>, ICrushResource, INotifyOwnerChanged
	{
		readonly IResourceLayer resourceLayer;
		readonly int resourceValue;

		PlayerResources playerResources;

		public ResourceCrusher(Actor self, ResourceCrusherInfo info)
			: base(info)
		{
			resourceLayer = self.World.WorldActor.Trait<IResourceLayer>();
			playerResources = self.Owner.PlayerActor.Trait<PlayerResources>();
			playerResources.Info.ResourceValues.TryGetValue(Info.ResourceType, out var resValue);
			resourceValue = resValue;
		}

		void ICrushResource.CrushResource(Actor self, CPos cell)
		{
			if (resourceValue == 0)
				return;

			var resourceAmount = resourceLayer.RemoveResource(Info.ResourceType, cell, byte.MaxValue);
			if (resourceAmount == 0)
				return;

			var value = Util.ApplyPercentageModifiers(resourceValue * resourceAmount, new int[] { Info.ValueModifier });

			playerResources.ChangeCash(value);
			if (Info.ShowTicks && self.Owner.IsAlliedWith(self.World.RenderPlayer))
				self.World.AddFrameEndTask(w => w.Add(new FloatingText(self.CenterPosition, self.Owner.Color, FloatingText.FormatCashTick(value), Info.TickLifetime)));

			if (Info.NotifyOtherActors)
			{
				foreach (var notify in self.World.ActorsWithTrait<INotifyResourceAccepted>())
				{
					if (notify.Actor.Owner != self.Owner)
						continue;

					notify.Trait.OnResourceAccepted(notify.Actor, self, Info.ResourceType, resourceAmount, resourceValue);
				}
			}
		}

		void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
		{
			playerResources = newOwner.PlayerActor.Trait<PlayerResources>();
		}
	}
}
