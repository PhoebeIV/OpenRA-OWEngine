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
	[Desc("Grants a condition to this actor when the player has stored resources.")]
	public class GrantConditionOnPlayerArmyValueInfo : TraitInfo
	{
		[FieldLoader.Require]
		[GrantedConditionReference]
		[Desc("Condition to grant.")]
		public readonly string Condition = null;

		[Desc("Enable condition when the Army Value of the player is above this amount.")]
		public readonly int Threshold = 0;

		public override object Create(ActorInitializer init) { return new GrantConditionOnPlayerArmyValue(this); }
	}

	public class GrantConditionOnPlayerArmyValue : INotifyCreated, INotifyOwnerChanged, ITick
	{
		readonly GrantConditionOnPlayerArmyValueInfo info;
		PlayerStatistics playerResources;

		int conditionToken = Actor.InvalidConditionToken;

		public GrantConditionOnPlayerArmyValue(GrantConditionOnPlayerArmyValueInfo info)
		{
			this.info = info;
		}

		void INotifyCreated.Created(Actor self)
		{
			playerResources = self.Owner.PlayerActor.Trait<PlayerStatistics>();
		}

		void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
		{
			playerResources = newOwner.PlayerActor.Trait<PlayerStatistics>();
		}

		void ITick.Tick(Actor self)
		{
			if (string.IsNullOrEmpty(info.Condition))
				return;

			var enabled = playerResources.ArmyValue > info.Threshold;
			if (enabled && conditionToken == Actor.InvalidConditionToken)
				conditionToken = self.GrantCondition(info.Condition);
			else if (!enabled && conditionToken != Actor.InvalidConditionToken)
				conditionToken = self.RevokeCondition(conditionToken);
		}
	}
}
