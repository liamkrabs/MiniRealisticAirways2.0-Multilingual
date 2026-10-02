using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class LinkHandler : MonoBehaviour, IPointerClickHandler, IEventSystemHandler
{
	private TMP_Text _textMeshPro;

	public string url;
	public string originalVersionUrl;

	internal string GetLinkUrl(Vector2 position, Camera eventCamera)
	{
		if (_textMeshPro == null) return null;
		int index = TMP_TextUtilities.FindIntersectingLink(_textMeshPro, position, eventCamera);
		if (index < 0) return null;
		string id = _textMeshPro.textInfo.linkInfo[index].GetLinkID();
		if (id == "original") return originalVersionUrl;
		return id == "docs" || id == "ENG" || id == "CHS" ? url : null;
	}

	private void Awake()
	{
		_textMeshPro = GetComponent<TMP_Text>();
		if (_textMeshPro != null)
		{
			_textMeshPro.raycastTarget = true;
		}
	}

	public void OnPointerClick(PointerEventData eventData)
	{
		if (eventData == null || eventData.button != PointerEventData.InputButton.Left) return;
		string target = GetLinkUrl(eventData.position, eventData.pressEventCamera);
		if (!string.IsNullOrWhiteSpace(target))
		{
			Application.OpenURL(target);
		}
	}
}
