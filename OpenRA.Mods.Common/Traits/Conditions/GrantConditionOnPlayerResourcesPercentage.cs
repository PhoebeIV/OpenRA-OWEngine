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
using OpenRA.Mods.Common.Traits;

namespace OpenRA.Mods.CA.Traits
{
	[Desc("Grants a condition to this actor when the player has stored funds (cash plus resources).")]
	public class GrantConditionOnPlayerResourcesPercentageInfo : TraitInfo
	{
		[FieldLoader.Require]
		[GrantedConditionReference]
		[Desc("Condition to grant.")]
		public readonly string Condition = null;

		[Desc("Enable condition when funds are greater than this percentage.")]
		public readonly int PercentageThreshold = 10;

		public override object Create(ActorInitializer init) { return new GrantConditionOnPlayerResourcesPercentage(this); }
	}

	public class GrantConditionOnPlayerResourcesPercentage : INotifyCreated, INotifyOwnerChanged, ITick
	{
		readonly GrantConditionOnPlayerResourcesPercentageInfo info;
		PlayerResources playerResources;

		int conditionToken = Actor.InvalidConditionToken;

		public GrantConditionOnPlayerResourcesPercentage(GrantConditionOnPlayerResourcesPercentageInfo info)
		{
			this.info = info;
		}

		void INotifyCreated.Created(Actor self)
		{
			playerResources = self.Owner.PlayerActor.Trait<PlayerResources>();
		}

		void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
		{
			playerResources = newOwner.PlayerActor.Trait<PlayerResources>();
		}

		void ITick.Tick(Actor self)
		{
			if (string.IsNullOrEmpty(info.Condition))
				return;

			var currentcash = playerResources.Resources;

			if ((currentcash >= info.PercentageThreshold * (long)playerResources.ResourceCapacity / 100) && conditionToken == Actor.InvalidConditionToken)
				conditionToken = self.GrantCondition(info.Condition);
			else if ((currentcash < info.PercentageThreshold * (long)playerResources.ResourceCapacity / 100) && conditionToken != Actor.InvalidConditionToken)
				conditionToken = self.RevokeCondition(conditionToken);
		}
	}
}
