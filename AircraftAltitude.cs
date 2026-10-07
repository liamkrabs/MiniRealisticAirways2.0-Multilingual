using System.Collections;
using UnityEngine;

namespace MiniRealisticAirways;

public class AircraftAltitude : Altitude
{
	public Aircraft aircraft_;

	public AltitudeLevel altitude_;

	public AltitudeLevel targetAltitude_;

	public TCASAction tcasAction_ = TCASAction.None;

	public bool altitudeDisabled_ = false;

	public IEnumerator enableAltitudeGaugeCoroutine_ = null;

	private AircraftAltitudeGauge altitudeGauge_;

	private const float TRANSITION_TIME = 5f;

	private IEnumerator transitioningCoroutine_ = null;

	private IEnumerator blinkCoroutine_ = null;

	private bool isEmergencyTransitioning_ = false;

	private int transitionRevision_;

	private Camera mainCamera_;

	private bool flightStateInitialized_;

	internal void RestoreFlightState(AltitudeLevel altitude, AltitudeLevel target)
	{
		flightStateInitialized_ = true;
		altitude_ = altitude;
		targetAltitude_ = target;
		tcasAction_ = TCASAction.Disabled;
	}

	public override string ToString()
	{
		if (altitude_ != targetAltitude_ && Animation.Blink())
		{
			return " ";
		}
		return Altitude.ToString(altitude_);
	}

	public bool CanLand()
	{
		// 建立进近只看指令，允许飞机在进近途中完成下降。
		return targetAltitude_ <= AltitudeLevel.Low;
	}

	public bool CanTouchDown()
	{
		return altitude_ == AltitudeLevel.Low && CanLand();
	}

	internal void CompleteTouchdown()
	{
		CancelAltitudeTransition();
		if (enableAltitudeGaugeCoroutine_ != null) StopCoroutine(enableAltitudeGaugeCoroutine_);
		transitioningCoroutine_ = null;
		blinkCoroutine_ = null;
		enableAltitudeGaugeCoroutine_ = null;
		isEmergencyTransitioning_ = false;
		altitude_ = AltitudeLevel.Ground;
		targetAltitude_ = AltitudeLevel.Ground;
		tcasAction_ = TCASAction.Disabled;
		altitudeGauge_?.DisableSpriteRenderer();
	}

	public IEnumerator EnableAltitudeGauge(AltitudeLevel altitude)
	{
		if (altitudeGauge_ == null)
		{
			yield break;
		}
		while (this != null && aircraft_ != null && altitudeGauge_ != null && !altitudeGauge_.Ready())
		{
			altitudeGauge_.TryInitialize();
			if (altitudeGauge_.InitializationFailed())
			{
				yield break;
			}
			yield return new WaitForFixedUpdate();
		}
		if (this != null && aircraft_ != null && altitudeGauge_ != null)
		{
			altitudeGauge_.UpdateGauge(altitude);
		}
	}

	internal bool CanReceiveAltitudeCommand => aircraft_ != null && !altitudeDisabled_
		&& altitude_ >= AltitudeLevel.Low && !aircraft_.OnTheGround
		&& aircraft_.state != Aircraft.State.TakingOff;

	public void AircraftClimb()
	{
		// Repeated upward input cannot stack on a pending climb, including a
		// two-level waypoint command. Reverse input is based on actual altitude.
		if (targetAltitude_ > altitude_) return;
		SetTargetAltitude(altitude_ < AltitudeLevel.High ? altitude_ + 1 : altitude_);
	}

	public void AircraftDescend()
	{
		if (targetAltitude_ < altitude_) return;
		SetTargetAltitude(altitude_ > AltitudeLevel.Low ? altitude_ - 1 : altitude_);
	}

	internal bool SetTargetAltitude(AltitudeLevel target)
	{
		if (!CanReceiveAltitudeCommand || isEmergencyTransitioning_) return false;
		return ReplaceAltitudeCommand(target, emergency: false);
	}

	public void EmergencyClimb(bool priority = false)
	{
		EmergencySetTargetAltitude(altitude_ < AltitudeLevel.High ? altitude_ + 1 : altitude_, priority);
	}

	public void EmergencyDescend()
	{
		EmergencySetTargetAltitude(altitude_ > AltitudeLevel.Low ? altitude_ - 1 : altitude_);
	}

	internal void EmergencySetTargetAltitude(AltitudeLevel target, bool priority = false)
	{
		if (Settings.DISABLE_TCAS || !CanReceiveAltitudeCommand || tcasAction_ == TCASAction.Disabled) return;
		if (isEmergencyTransitioning_ && !priority) return;
		ReplaceAltitudeCommand(target, emergency: true);
	}

	private bool ReplaceAltitudeCommand(AltitudeLevel target, bool emergency)
	{
		if (target < AltitudeLevel.Low || target > AltitudeLevel.High) return false;
		if (target == targetAltitude_ && (transitioningCoroutine_ != null || target == altitude_))
		{
			// Safety may take ownership of an existing command without restarting
			// its timer. Repeated identical commands never postpone completion.
			if (emergency && target != altitude_) SetEmergencyAction(target);
			return true;
		}
		CancelAltitudeTransition();
		targetAltitude_ = target;
		if (target != altitude_)
		{
			if (emergency) SetEmergencyAction(target);
			AltitudeTransition();
		}
		return true;
	}

	private void SetEmergencyAction(AltitudeLevel target)
	{
		isEmergencyTransitioning_ = true;
		tcasAction_ = target > altitude_ ? TCASAction.Climb : TCASAction.Descend;
	}

	private void CancelAltitudeTransition()
	{
		transitionRevision_++;
		if (transitioningCoroutine_ != null) StopCoroutine(transitioningCoroutine_);
		if (blinkCoroutine_ != null) StopCoroutine(blinkCoroutine_);
		transitioningCoroutine_ = null;
		blinkCoroutine_ = null;
		isEmergencyTransitioning_ = false;
		if (tcasAction_ != TCASAction.Disabled) tcasAction_ = TCASAction.None;
		if (altitudeGauge_ != null && altitudeGauge_.Ready()) altitudeGauge_.UpdateGauge(altitude_);
	}

	public bool IsLanding()
	{
		return aircraft_ != null && aircraft_.state == Aircraft.State.Landing;
	}

	private void Start()
	{
		if (aircraft_ == null)
		{
			return;
		}
		mainCamera_ = Camera.main;
		altitudeGauge_ = aircraft_.GetComponent<AircraftAltitudeGauge>();
		if (altitudeGauge_ == null)
		{
			altitudeGauge_ = aircraft_.gameObject.AddComponent<AircraftAltitudeGauge>();
		}
		altitudeGauge_.aircraft_ = aircraft_;
		if (flightStateInitialized_)
		{
			if (altitude_ != targetAltitude_) AltitudeTransition();
			return;
		}
		flightStateInitialized_ = true;
		if (aircraft_.direction == Aircraft.Direction.Outbound)
		{
			altitude_ = AltitudeLevel.Ground;
			targetAltitude_ = AltitudeLevel.Low;
		}
		if (aircraft_.direction == Aircraft.Direction.Inbound)
		{
			if (aircraft_ is AirForceOneEVAircraft)
			{
				altitude_ = AltitudeLevel.Low;
				targetAltitude_ = AltitudeLevel.Low;
			}
			else
			{
				altitude_ = AltitudeLevel.High;
				targetAltitude_ = AltitudeLevel.High;
			}
		}
	}

	private void Update()
	{
		if (aircraft_ == null)
		{
			Object.Destroy(base.gameObject);
			return;
		}
		TakeoffTouchdownArrivalProcess();
	}

	private void AltitudeTransition()
	{
		if (transitioningCoroutine_ == null)
		{
			transitioningCoroutine_ = AltitudeTransitionCoroutine(targetAltitude_);
			StartCoroutine(transitioningCoroutine_);
		}
	}


	private void TakeoffTouchdownArrivalProcess()
	{
		if (altitude_ == AltitudeLevel.Ground && aircraft_.direction == Aircraft.Direction.Outbound && (aircraft_.state == Aircraft.State.Flying || aircraft_.state == Aircraft.State.HeadingAfterReachingWaypoint))
		{
			CancelAltitudeTransition();
			altitude_ = AltitudeLevel.Low;
			targetAltitude_ = AltitudeLevel.Low;
			if (enableAltitudeGaugeCoroutine_ == null)
			{
				enableAltitudeGaugeCoroutine_ = EnableAltitudeGauge(altitude_);
				StartCoroutine(enableAltitudeGaugeCoroutine_);
			}
		}
		if (altitude_ != AltitudeLevel.Ground && AircraftState.DisableStateOnTouchedDown(aircraft_))
		{
			CompleteTouchdown();
		}
		if (mainCamera_ == null)
		{
			mainCamera_ = Camera.main;
		}
		bool flag = AircraftViewport.IsVisible(aircraft_, mainCamera_);
		if (aircraft_.direction == Aircraft.Direction.Inbound && flag && enableAltitudeGaugeCoroutine_ == null)
		{
			enableAltitudeGaugeCoroutine_ = EnableAltitudeGauge(altitude_);
			StartCoroutine(enableAltitudeGaugeCoroutine_);
		}
	}

	private IEnumerator AltitudeTransitionCoroutine(AltitudeLevel targetAltitude)
	{
		int revision = transitionRevision_;
		// 仪表只是显示层；即使缺失或初始化失败，高度和避撞仍按时推进。
		while (aircraft_ != null)
		{
			// Preserve gradual safety climbs/descents: one level per interval.
			// An ordinary absolute waypoint clearance may cross two levels in one.
			AltitudeLevel stepTarget = isEmergencyTransitioning_ && altitude_ != targetAltitude
				? (altitude_ < targetAltitude ? altitude_ + 1 : altitude_ - 1)
				: targetAltitude;
			blinkCoroutine_ = altitudeGauge_ != null && altitudeGauge_.TryInitialize()
				? altitudeGauge_.GetTransitioningCoroutine(altitude_, stepTarget)
				: null;
			if (blinkCoroutine_ != null)
			{
				StartCoroutine(blinkCoroutine_);
			}
			yield return new WaitForSeconds(TRANSITION_TIME);
			if (revision != transitionRevision_) yield break;
			if (aircraft_ == null)
			{
				transitioningCoroutine_ = null;
				isEmergencyTransitioning_ = false;
				yield break;
			}
			// 同帧内触地可早于 Update：过期的下降/TCAS协程不得恢复空中高度。
			if (AircraftState.DisableStateOnTouchedDown(aircraft_))
			{
				CompleteTouchdown();
				yield break;
			}
			altitude_ = stepTarget;
			if (altitude_ == targetAltitude_ && tcasAction_ != TCASAction.Disabled)
			{
				tcasAction_ = TCASAction.None;
			}
			if (blinkCoroutine_ != null)
			{
				StopCoroutine(blinkCoroutine_);
				blinkCoroutine_ = null;
			}
			if (altitudeGauge_ != null && altitudeGauge_.Ready())
			{
				altitudeGauge_.UpdateGauge(altitude_);
			}
			if (altitude_ == targetAltitude_)
			{
				transitioningCoroutine_ = null;
				isEmergencyTransitioning_ = false;
				yield break;
			}
			targetAltitude = targetAltitude_;
		}
		transitioningCoroutine_ = null;
		isEmergencyTransitioning_ = false;
	}

	private void OnDestroy()
	{
		if (transitioningCoroutine_ != null)
		{
			StopCoroutine(transitioningCoroutine_);
		}
		if (blinkCoroutine_ != null)
		{
			StopCoroutine(blinkCoroutine_);
		}
		transitioningCoroutine_ = null;
		blinkCoroutine_ = null;
		altitudeGauge_ = null;
		aircraft_ = null;
	}
}
