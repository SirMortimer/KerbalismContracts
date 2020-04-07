﻿using System;
using FinePrint;
using Contracts;
using KERBALISM;
using KSP.Localization;

namespace KerbalismContracts
{
	public class AboveWaypointState: SubRequirementState
	{
		public double elevation;
		public double distance;
		public bool distanceMet;
		public bool changeRequirementsMet;
		public double distanceChange;
		public double radialVelocity;
		internal bool radialVelocityRequirementMet;
		internal double distance10sago;
		internal double elev10sago;
	}

	public class AboveWaypoint : SubRequirement
	{
		private double min_elevation;

		private double min_distance;
		private double max_distance;

		// min distance change will be ORed with radial velocity change requirements.
		// so you either need a minimal distance change, OR a minimal radial velocity.
		private double min_relative_speed;

		// radial velocities are in degrees per minute
		private double min_radial_velocity;
		private double max_radial_velocity;

		public AboveWaypoint(string type, KerbalismContractRequirement requirement, ConfigNode node) : base(type, requirement)
		{
			min_elevation = Lib.ConfigValue(node, "min_elevation", 0.0);
			min_radial_velocity = Lib.ConfigValue(node, "min_radial_velocity", 0.0);
			max_radial_velocity = Lib.ConfigValue(node, "max_radial_velocity", 0.0);
			min_distance = Lib.ConfigValue(node, "min_distance", 0.0);
			max_distance = Lib.ConfigValue(node, "max_distance", 0.0);
			min_relative_speed = Lib.ConfigValue(node, "min_relative_speed", 0.0);
		}

		public override string GetTitle(EvaluationContext context)
		{
			string waypointName = "waypoint";
			if (context?.waypoint != null)
				waypointName = context.waypoint.name;

			string result = Localizer.Format("Min. <<1>>° above <<2>>", min_elevation.ToString("F1"), waypointName);

			if (min_radial_velocity > 0)
				result += ", " + Localizer.Format("min. radial vel. <<1>> °/m", min_radial_velocity.ToString("F1"));

			if (max_radial_velocity > 0)
				result += ", " + Localizer.Format("max. radial vel. <<1>> °/m", max_radial_velocity.ToString("F1"));

			if (min_distance > 0)
				result += ", " + Localizer.Format("min. distance <<1>>", Lib.HumanReadableDistance(min_distance));

			if (max_distance > 0)
				result += ", " + Localizer.Format("max. distance <<1>>", Lib.HumanReadableDistance(max_distance));

			if (min_relative_speed > 0)
				result += ", " + Localizer.Format("min. relative vel. <<1>>", Lib.HumanReadableSpeed(min_relative_speed));

			return result;
		}

		internal override bool NeedsWaypoint()
		{
			return true;
		}

		internal override bool CouldBeCandiate(Vessel vessel, EvaluationContext context)
		{
			if (context.waypoint == null)
				return false;
			if (context.waypoint.celestialBody != vessel.mainBody)
				return false;

			var orbit = vessel.orbit;
			if (orbit == null)
				return false;

			return true;
		}

		internal override SubRequirementState VesselMeetsCondition(Vessel vessel, EvaluationContext context)
		{
			AboveWaypointState state = new AboveWaypointState();

			state.elevation = GetElevation(vessel, context);
			state.distance = GetDistance(vessel, context);

			// TODO determine line of sight obstruction (there may be an occluding body)

			bool meetsCondition = state.elevation >= min_elevation;

			if (min_distance > 0 || max_distance > 0)
			{
				state.distanceMet = true;
				if (min_distance > 0)
					state.distanceMet &= min_distance <= state.distance;
				if (max_distance > 0)
					state.distanceMet &= max_distance >= state.distance;

				meetsCondition &= state.distanceMet;
			}

			state.changeRequirementsMet = true;

			if (min_relative_speed > 0 || min_radial_velocity > 0 || max_radial_velocity > 0)
				state.changeRequirementsMet = false;
			
			if (min_relative_speed > 0)
			{
				state.distance10sago = GetDistance(vessel, context, 10);
				state.distanceChange = Math.Abs((state.distance10sago - state.distance) / 10.0);
				state.changeRequirementsMet |= state.distanceChange >= min_relative_speed;
			}

			if (min_radial_velocity > 0 || max_radial_velocity > 0)
			{
				state.elev10sago = GetElevation(vessel, context, 10);
				state.radialVelocity = Math.Abs((state.elev10sago - state.elevation) * 6.0); // radial velocity is in degrees/minute

				state.radialVelocityRequirementMet = true;
				if (min_radial_velocity > 0)
					state.radialVelocityRequirementMet &= state.radialVelocity >= min_radial_velocity;
				if (max_radial_velocity > 0)
					state.radialVelocityRequirementMet &= state.radialVelocity <= max_radial_velocity;

				state.changeRequirementsMet |= state.radialVelocityRequirementMet;
			}

			meetsCondition &= state.changeRequirementsMet;

			state.requirementMet = meetsCondition;

			Utils.LogDebug($"{context.now.ToString("F1")} {meetsCondition}: el {state.elevation.ToString("F1")}° ∆el {state.radialVelocity.ToString("F1")}°/m ∆d {state.distanceChange.ToString("F1")}m/s ");

			return state;
		}

		internal override string GetLabel(Vessel vessel, EvaluationContext context, SubRequirementState state)
		{
			string label = string.Empty;

			AboveWaypointState wpState = (AboveWaypointState)state;

			string elevationString = Lib.BuildString(wpState.elevation.ToString("F1"), " °");
			if (wpState.elevation < min_elevation)
				label = Localizer.Format("elevation above <<1>>: <<2>>",
					context.waypoint.name, Lib.Color(elevationString, Lib.Kolor.Red));
			else if (wpState.elevation - (90 - min_elevation) / 3 < min_elevation)
				label = Localizer.Format("elevation above <<1>>: <<2>>",
					context.waypoint.name, Lib.Color(elevationString, Lib.Kolor.Yellow));
			else
				label = Localizer.Format("elevation above <<1>>: <<2>>",
					context.waypoint.name, Lib.Color(elevationString, Lib.Kolor.Green));

			if (min_distance > 0 || max_distance > 0)
			{
				label += "\n\t" + Localizer.Format("distance: <<1>>", Lib.Color(Lib.HumanReadableDistance(wpState.distance),
					wpState.distanceMet ? Lib.Kolor.Green : Lib.Kolor.Red));
			}

			if (min_relative_speed > 0)
			{
				label += "\n\t" + Localizer.Format("relative velocity: <<1>>", Lib.Color(Lib.HumanReadableSpeed(wpState.distanceChange),
					wpState.distanceChange >= min_relative_speed ? Lib.Kolor.Green : Lib.Kolor.Red));
			}

			if (min_radial_velocity > 0 || max_radial_velocity > 0)
			{
				label += "\n\t" + Localizer.Format("angular velocity: <<1>>", Lib.Color(wpState.radialVelocity.ToString("F1") + " °/m",
					wpState.radialVelocityRequirementMet ? Lib.Kolor.Green : Lib.Kolor.Red));
			}

			return label;
		}

		private double GetElevation(Vessel vessel, EvaluationContext context, int secondsAgo = 0)
		{
			Vector3d waypointPosition = context.WaypointSurfacePosition(secondsAgo);
			Vector3d bodyPosition = context.BodyPosition(context.waypoint.celestialBody, secondsAgo);
			Vector3d vesselPosition = context.VesselPosition(vessel, secondsAgo);

			var a = Vector3d.Angle(vesselPosition - bodyPosition, waypointPosition - bodyPosition);
			var b = Vector3d.Angle(waypointPosition - vesselPosition, bodyPosition - vesselPosition);

			// a + b + elevation = 90 degrees
			return 90.0 - a - b;
		}

		private double GetDistance(Vessel vessel, EvaluationContext context, int secondsAgo = 0)
		{
			var waypointPosition = context.WaypointSurfacePosition(secondsAgo);
			Vector3d vesselPosition = context.VesselPosition(vessel, secondsAgo);
			var v = vesselPosition - waypointPosition;
			return v.magnitude;
		}
	}
}
