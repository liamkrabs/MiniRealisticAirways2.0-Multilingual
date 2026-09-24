using UnityEngine;

namespace MiniRealisticAirways;

public class WaypointNameInput : MonoBehaviour
{
	public string text = "";

	public PlaceableWaypoint waypoint_;

	public bool active;

	private const int MAX_LENGTH = 5;
	private string originalText_;
	private static bool resumeOnConfirm_;
	private static bool imeOwned_;
	private static IMECompositionMode previousImeMode_;

	private static readonly System.Collections.Generic.List<WaypointNameInput> inputs_ = new System.Collections.Generic.List<WaypointNameInput>();
	private static WaypointNameInput focused_;
	private static WaypointNameInput hovered_;
	private static Camera mainCamera_;
	private static int processedFrame_ = -1;
	private static bool consumedFrame_;
	// 会话在本帧结束过。原版把 Space 绑定为暂停（LevelManager.Update），命名必须
	// 通过 OnPauseTimeBtnPressed 前缀接管同一个按键；该戳记防止同帧"结束后又立即
	// 重新开启"或把结束用的按键再交给暂停。
	private static int sessionEndedFrame_ = -1;

	internal static bool AnyActive
	{
		get { ProcessInput(); return focused_ != null || consumedFrame_; }
	}

	internal static Transform HoveredTarget
	{
		get { ProcessInput(); return hovered_ == null ? null : hovered_.waypoint_.transform; }
	}

	private void OnEnable() { active = false; if (!inputs_.Contains(this)) inputs_.Add(this); }
	private void OnDisable()
	{
		active = false;
		if (focused_ == this) EndSession();
		if (hovered_ == this) hovered_ = null;
		inputs_.Remove(this);
		if (inputs_.Count == 0) mainCamera_ = null;
		if (focused_ == null) RestoreIme();
	}
	private void OnDestroy() { OnDisable(); }
	private void Update() { ProcessInput(); }
	private void OnApplicationFocus(bool hasFocus)
	{
		if (!hasFocus && focused_ == this) { EndSession(); RestoreIme(); }
	}

	// All consumers resolve the same target and naming state before reading flight keys.
	// This is independent of Unity's order of Update callbacks.
	private static void ProcessInput()
	{
		if (processedFrame_ == Time.frameCount) return;
		processedFrame_ = Time.frameCount;
		consumedFrame_ = sessionEndedFrame_ == Time.frameCount;
		if (focused_ == null && !consumedFrame_) RestoreIme();
		hovered_ = null;
		float best = float.PositiveInfinity;
		foreach (WaypointNameInput input in inputs_)
		{
			if (input == null || !input.isActiveAndEnabled || input.waypoint_ == null || input.waypoint_.Invisible || !(input.waypoint_ is BaseWaypointAutoHeading)) continue;
			if (PointerUtility.TryGetDistance(input.waypoint_.transform, ref mainCamera_, out float distance)
				&& (distance < best || (distance == best && (hovered_ == null || input.GetInstanceID() < hovered_.GetInstanceID()))))
			{
				best = distance;
				hovered_ = input;
			}
		}
		if (focused_ != null && (!focused_.isActiveAndEnabled || focused_.waypoint_ == null || focused_.waypoint_.Invisible || !focused_.active))
		{
			EndSession();
		}
		if (focused_ != null)
		{
			consumedFrame_ = true;
			if (!CanEdit()) EndSession();
			else if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
			{
				bool resume = resumeOnConfirm_;
				EndSession();
				// Placement owns its pause and blocks manual time control. Never release that lock.
				if (resume) TimeManager.Instance.Resume(isManual: true);
			}
			else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Escape))
			{
				EndSession(cancel: Input.GetKeyDown(KeyCode.Escape));
			}
			else focused_.Type(); // Continue editing after the pointer leaves the waypoint.
		}
		else if (!consumedFrame_ && Input.GetKeyDown(KeyCode.Space) && CanEdit())
		{
			WaypointNameInput target = hovered_;
			// A moving placement preview may not yet have followed this frame's pointer.
			if (WaypointPropsManager.Instance != null && WaypointPropsManager.Instance.HasPlacingProps)
			{
				target = inputs_.Find(input => input != null && input.isActiveAndEnabled
					&& input.waypoint_ != null && !input.waypoint_.Invisible
					&& input.waypoint_ is BaseWaypointAutoHeading
					&& input.waypoint_ == WaypointPropsManager.Instance.currentPlacingWaypoint);
			}
			if (target != null)
			{
				bool hardcore = MapManager.gameMode == GameMode.HardCore;
				if (!hardcore && Time.timeScale > 0f) TimeManager.Instance.Pause(isManual: true);
				// Hardcore permits naming while traffic keeps running. Never change its speed.
				resumeOnConfirm_ = !hardcore && Time.timeScale == 0f;
				if (!imeOwned_)
				{
					previousImeMode_ = Input.imeCompositionMode;
					imeOwned_ = true;
				}
				Input.imeCompositionMode = IMECompositionMode.Off;
				focused_ = target;
				focused_.originalText_ = focused_.text ?? string.Empty;
				focused_.active = true;
				consumedFrame_ = true;
			}
		}
	}

	private static bool CanEdit()
	{
		if (TimeManager.Instance == null || LevelManager.Instance == null
			|| LevelManager.Instance.ShowingPauseMenu || LevelManager.Instance.ShowingOptions
			|| (GameOverManager.Instance != null && GameOverManager.Instance.GameOverFlag)
			|| (UpgradeManager.Instance != null && UpgradeManager.Instance.HasShowingUpgradePanel)) return false;
		return !TimeManager.Instance.IsManualTimeControlBlocked()
			|| (WaypointPropsManager.Instance != null && WaypointPropsManager.Instance.HasPlacingProps);
	}

	/// <summary>
	/// LevelManager.OnPauseTimeBtnPressed 的键盘 Space 仲裁入口。
	/// ProcessInput resolves Space once per frame, before any flight/time shortcut consumer.
	/// Suppress the native toggle on entry/exit so it cannot undo the requested pause/resume.
	/// </summary>
	internal static bool ConsumePauseKey()
	{
		ProcessInput();
		return consumedFrame_ || focused_ != null;
	}

	private static void EndSession(bool cancel = false)
	{
		if (focused_ != null)
		{
			if (cancel) focused_.text = focused_.originalText_;
			focused_.originalText_ = null;
			focused_.active = false;
			focused_ = null;
		}
		consumedFrame_ = true;
		sessionEndedFrame_ = Time.frameCount;
		resumeOnConfirm_ = false;
		// Keep IME disabled through the confirming Space/click frame.
	}

	private static void RestoreIme()
	{
		if (!imeOwned_) return;
		Input.imeCompositionMode = previousImeMode_;
		imeOwned_ = false;
	}

	private void Type()
	{
		text ??= string.Empty;
		if (Input.GetKeyDown(KeyCode.Backspace) && text.Length > 0)
		{
			text = text.Substring(0, text.Length - 1);
			return;
		}
		Input.imeCompositionMode = IMECompositionMode.Off;
		// Read the letter/digit keys even with a Chinese input method selected.
		// Prefer key events to committed text so an IME candidate cannot replace the name.
		bool hadKey = false;
		for (int i = 0; i < 26; i++)
		{
			if (!Input.GetKeyDown((KeyCode)((int)KeyCode.A + i))) continue;
			hadKey = true;
			AppendAscii((char)('A' + i));
		}
		for (int i = 0; i < 10; i++)
		{
			if (!Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha0 + i))
				&& !Input.GetKeyDown((KeyCode)((int)KeyCode.Keypad0 + i))) continue;
			hadKey = true;
			AppendAscii((char)('0' + i));
		}
		if (hadKey) return;
		string inputString = Input.inputString;
		for (int i = 0; i < inputString.Length; i++)
		{
			AppendAscii(inputString[i]);
		}
	}

	private void AppendAscii(char c)
	{
		// Normalize full-width Latin letters/digits; reject Han characters and symbols.
		if (c >= '\uFF01' && c <= '\uFF5E') c = (char)(c - 0xFEE0);
		if (c >= 'a' && c <= 'z') c = (char)(c - 'a' + 'A');
		if (text.Length < MAX_LENGTH && ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')))
			text += c;
	}
}
