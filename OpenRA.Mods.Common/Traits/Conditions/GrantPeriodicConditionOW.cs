#region Copyright & License Information
/*
 * Copyright 2015- OpenRA.Mods.AS Developers (see AUTHORS)
 * This file is a part of a third-party plugin for OpenRA, which is
 * free software. It is made available to you under the terms of the
 * GNU General Public License as published by the Free Software
 * Foundation. For more information, see COPYING.
 */
#endregion

using System;
using System.Linq;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[Desc("Grants a condition periodically. Can be modified with PeriodicConditionCooldownMultiplier and PeriodicConditionActiveMultiplier")]
	public class GrantPeriodicConditionOWInfo : PausableConditionalTraitInfo
	{
		[GrantedConditionReference]
		[FieldLoader.Require]
		[Desc("The condition to grant.")]
		public readonly string Condition = null;

		[Desc("The time (in ticks) with the condition being disabled.")]
		public readonly int CooldownDuration = 1000;

		[Desc("The time (in ticks) with the condition being enabled.")]
		public readonly int ActiveDuration = 100;

		public readonly bool StartsGranted = false;

		public readonly bool ShowSelectionBar = false;
		public readonly Color CooldownColor = Color.DarkRed;
		public readonly Color ActiveColor = Color.DarkMagenta;

		public override object Create(ActorInitializer init) { return new GrantPeriodicConditionOW(init, this); }
	}

	public class GrantPeriodicConditionOW : PausableConditionalTrait<GrantPeriodicConditionOWInfo>, ISelectionBar, ITick, ISync
	{
		readonly Actor self;
		readonly GrantPeriodicConditionOWInfo info;
		readonly Lazy<IPeriodicConditionCooldownModifier[]> cooldownModifiers;
		readonly Lazy<IPeriodicConditionActiveModifier[]> activeModifiers;

		[VerifySync]
		int ticks;

		int cooldown, active;
		bool isSuspended;
		int token = Actor.InvalidConditionToken;

		bool IsEnabled { get { return token != Actor.InvalidConditionToken; } }

		public GrantPeriodicConditionOW(ActorInitializer init, GrantPeriodicConditionOWInfo info)
			: base(info)
		{
			self = init.Self;
			this.info = info;
			cooldownModifiers = Exts.Lazy(() => self.TraitsImplementing<IPeriodicConditionCooldownModifier>().ToArray());
			activeModifiers = Exts.Lazy(() => self.TraitsImplementing<IPeriodicConditionActiveModifier>().ToArray());
		}

		void SetDefaultState()
		{
			if (info.StartsGranted)
			{
				ticks = GetActiveModifier();
				active = GetActiveModifier();
				if (info.StartsGranted != IsEnabled)
					EnableCondition();
			}
			else
			{
				ticks = GetCooldownModifier();
				cooldown = GetCooldownModifier();
				if (info.StartsGranted != IsEnabled)
					DisableCondition();
			}

			isSuspended = false;
		}

		protected override void Created(Actor self)
		{
			if (!IsTraitDisabled)
				SetDefaultState();

			base.Created(self);
		}

		void ITick.Tick(Actor self)
		{
			if (!IsTraitDisabled && !IsTraitPaused && --ticks < 0)
			{
				if (IsEnabled)
				{
					ticks = GetCooldownModifier();
					cooldown = GetCooldownModifier();
					DisableCondition();
				}
				else
				{
					ticks = GetActiveModifier();
					active = GetActiveModifier();
					EnableCondition();
				}
			}
		}

		protected override void TraitEnabled(Actor self)
		{
			SetDefaultState();
		}

		protected override void TraitDisabled(Actor self)
		{
			if (IsEnabled)
				DisableCondition();
		}

		protected override void TraitPaused(Actor self)
		{
			if (IsEnabled)
			{
				DisableCondition();
				isSuspended = true;
			}
		}

		protected override void TraitResumed(Actor self)
		{
			if (isSuspended)
			{
				EnableCondition();
				isSuspended = false;
			}
		}

		void EnableCondition()
		{
			if (token == Actor.InvalidConditionToken)
				token = self.GrantCondition(info.Condition);
		}

		void DisableCondition()
		{
			if (token != Actor.InvalidConditionToken)
				token = self.RevokeCondition(token);
		}

		public int GetActiveModifier()
		{
			return Util.ApplyPercentageModifiers(Info.ActiveDuration, activeModifiers.Value.Select(m => m.GetPeriodicConditionActiveModifier()));
		}

		public int GetCooldownModifier()
		{
			return Util.ApplyPercentageModifiers(Info.CooldownDuration, cooldownModifiers.Value.Select(m => m.GetPeriodicConditionCooldownModifier()));
		}

		float ISelectionBar.GetValue()
		{
			if (!info.ShowSelectionBar)
				return 0f;

			return IsEnabled
				? (float)(active - ticks) / active
					: (float)(cooldown - ticks) / cooldown;
		}

		bool ISelectionBar.DisplayWhenEmpty { get { return info.ShowSelectionBar; } }

		Color ISelectionBar.GetColor() { return IsEnabled ? info.ActiveColor : info.CooldownColor; }
	}
}
