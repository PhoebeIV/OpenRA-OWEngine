#region Copyright & License Information
/*
 * Copyright The OpenSA Developers (see CREDITS)
 * This file is part of OpenSA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Collections.Generic;
using System.Linq;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.OpenSA.Traits.Conditions
{
	public class GrantConditionWhileProducingInfo : PausableConditionalTraitInfo, Requires<ProductionInfo>
	{
		[FieldLoader.Require]
		[GrantedConditionReference]
		public readonly string Condition = null;

		[Desc("Queues that should be producing for this overlay to render.")]
		public readonly HashSet<string> Queues = new();

		public override object Create(ActorInitializer init)
		{
			return new GrantConditionWhileProducing(init.Self, this);
		}
	}

	public class GrantConditionWhileProducing : PausableConditionalTrait<GrantConditionWhileProducingInfo>, INotifyOwnerChanged
	{
		readonly Actor self;
		readonly GrantConditionWhileProducingInfo info;
		readonly ProductionInfo[] productionQueues;
		ProductionQueue[] queues;
		int token = Actor.InvalidConditionToken;
		bool IsProducing
		{
			get { return queues != null && queues.Any(q => q.Enabled && q.AllQueued().Any(i => !i.Paused && i.Started) && q.MostLikelyProducer().Actor == self); }
		}

		public GrantConditionWhileProducing(Actor self, GrantConditionWhileProducingInfo info)
			: base(info)
		{
			var hasCondition = token != Actor.InvalidConditionToken;
			this.self = self;
			this.info = info;
			
			productionQueues = self.Info.TraitInfos<ProductionInfo>().ToArray();

			if (!hasCondition && IsProducing)
				token = self.GrantCondition(info.Condition);
			else if (hasCondition && !IsProducing)
				token = self.RevokeCondition(token);
		}

		void CacheQueues(Actor self)
		{
			// Per-actor production
			queues = self.TraitsImplementing<ProductionQueue>()
				.Where(q =>
					productionQueues.Any(p => p.Produces.Contains(q.Info.Type)) &&
					(Info.Queues.Count == 0 || Info.Queues.Contains(q.Info.Type)))
				.ToArray();

			if (queues.Length == 0)
			{
				// Player-wide production
				queues = self.Owner.PlayerActor.TraitsImplementing<ProductionQueue>()
					.Where(q =>
						productionQueues.Any(p => p.Produces.Contains(q.Info.Type)) &&
						(Info.Queues.Count == 0 || Info.Queues.Contains(q.Info.Type)))
					.ToArray();
			}
		}

		protected override void TraitEnabled(Actor self)
		{
			CacheQueues(self);
		}
		void INotifyOwnerChanged.OnOwnerChanged(Actor self, Player oldOwner, Player newOwner)
		{
			self.World.AddFrameEndTask(w => CacheQueues(self));
		}
	}
}
