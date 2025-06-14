﻿using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Linq;

public class InfiniteScroll : UIBehaviour
{
	[SerializeField] private RectTransform[] itemPrototypes;

	[SerializeField, Range(0, 30)] int instantateItemCount = 9;

	[SerializeField] private Direction direction;

	[SerializeField] private bool limitScrollRange = false;

	[SerializeField] private float minScrollPosition = 0f;

	[SerializeField] private float maxScrollPosition = 1000f;

	public OnItemPositionChange onUpdateItem = new OnItemPositionChange();

	[System.NonSerialized] public LinkedList<RectTransform> itemList = new LinkedList<RectTransform>();

	protected float diffPreFramePosition = 0;

	protected int currentItemNo = 0;

	[SerializeField] private GameObject Star4DetailPanel; // 拡大表示用のパネル
	[SerializeField] private Transform Star4DisplayArea; // 拡大表示用のエリア

	private GameObject currentDisplayedItem; // 現在表示中のアイテム
	

	private void ShowItemDetails(GameObject item)
	{
    	// パネルを表示
    	Star4DetailPanel.SetActive(true);

    	// 既存の表示アイテムを削除
    	if (currentDisplayedItem != null)
    	{
        	Destroy(currentDisplayedItem);
    	}

    	// アイテムを拡大表示エリアにインスタンス化
    	currentDisplayedItem = Instantiate(item, Star4DisplayArea);
    	currentDisplayedItem.transform.localScale = Vector3.one * 3.0f; // 拡大倍率を設定
    	currentDisplayedItem.transform.localPosition = Vector3.zero; // 座標を (0, 0) に設定

    	// 必要に応じてボタンやイベントを無効化
    	Button button = currentDisplayedItem.GetComponent<Button>();
    	if (button != null)
    	{
        	Destroy(button); // 拡大表示中はボタンを無効化
    	}
	}

	// 拡大表示を閉じる処理
	public void CloseItemDetails()
	{
    	// パネルを非表示
    	Star4DetailPanel.SetActive(false);

    	// 表示中のアイテムを削除
    	if (currentDisplayedItem != null)
    	{
        	Destroy(currentDisplayedItem);
    	}
	}

	public enum Direction
	{
		Vertical,
		Horizontal,
	}

	// cache component

	private RectTransform _rectTransform;
	protected RectTransform rectTransform {
		get {
			if(_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
			return _rectTransform;
		}
	}

	private float anchoredPosition
	{
		get {
			return direction == Direction.Vertical ? -rectTransform.anchoredPosition.y : rectTransform.anchoredPosition.x;
		}
		set {
			if (direction == Direction.Vertical)
				rectTransform.anchoredPosition = new Vector2(rectTransform.anchoredPosition.x, -value);
			else
				rectTransform.anchoredPosition = new Vector2(value, rectTransform.anchoredPosition.y);
		}
	}

	private float _itemScale = -1;
	public float itemScale {
		get {
			if(itemPrototypes != null && itemPrototypes.Length > 0 && _itemScale == -1) {
				_itemScale = direction == Direction.Vertical ? itemPrototypes[0].sizeDelta.y : itemPrototypes[0].sizeDelta.x;
			}
			return _itemScale;
		}
	}

	protected override void Start ()
	{
		var controllers = GetComponents<MonoBehaviour>()
				.Where(item => item is IInfiniteScrollSetup)
				.Select(item => item as IInfiniteScrollSetup)
				.ToList();

		// create items

		var scrollRect = GetComponentInParent<ScrollRect>();
		scrollRect.horizontal = direction == Direction.Horizontal;
		scrollRect.vertical = direction == Direction.Vertical;
		scrollRect.content = rectTransform;

		// スクロール範囲の制限を設定
		if (limitScrollRange)
		{
			scrollRect.onValueChanged.AddListener((Vector2 value) => {
				if (direction == Direction.Horizontal)
				{
					float normalizedPosition = value.x;
					float contentSize = rectTransform.rect.width;
					float viewportSize = scrollRect.viewport.rect.width;
					float maxScroll = contentSize - viewportSize;
					
					float currentPosition = normalizedPosition * maxScroll;
					if (currentPosition < minScrollPosition)
					{
						scrollRect.horizontalNormalizedPosition = minScrollPosition / maxScroll;
					}
					else if (currentPosition > maxScrollPosition)
					{
						scrollRect.horizontalNormalizedPosition = maxScrollPosition / maxScroll;
					}
				}
			});
		}

		// すべてのプロトタイプを非表示にする
		foreach (var prototype in itemPrototypes)
		{
			prototype.gameObject.SetActive(false);
		}
		
		for(int i = 0; i < instantateItemCount; i++) {
			int prototypeIndex = i % itemPrototypes.Length;
			var item = GameObject.Instantiate(itemPrototypes[prototypeIndex]) as RectTransform;
			item.SetParent(transform, false);
			item.name = i.ToString();
			item.anchoredPosition = direction == Direction.Vertical ? new Vector2(0, -itemScale * i) : new Vector2(itemScale * i, 0);
			itemList.AddLast(item);

			item.gameObject.SetActive(true);

			// タップイベントを設定
        	AddTapEvent(item.gameObject);

			foreach(var controller in controllers) {
				controller.OnUpdateItem(i, item.gameObject);
			}
		}

		foreach(var controller in controllers){
			controller.OnPostSetupItems();
		}
	}

	void Update()
	{
		if (itemList.First == null) {
			return;
		}

		while(anchoredPosition - diffPreFramePosition  < -itemScale * 2) {
			diffPreFramePosition -= itemScale;

			var item = itemList.First.Value;
			itemList.RemoveFirst();
			itemList.AddLast(item);

			var pos = itemScale * instantateItemCount + itemScale * currentItemNo;
			item.anchoredPosition = (direction == Direction.Vertical) ? new Vector2(0, -pos) : new Vector2(pos, 0);

			int prototypeIndex = (currentItemNo + instantateItemCount) % itemPrototypes.Length;
			onUpdateItem.Invoke(currentItemNo + instantateItemCount, item.gameObject);

			currentItemNo++;
		}

		while(anchoredPosition - diffPreFramePosition > 0) {
			diffPreFramePosition += itemScale;

			var item = itemList.Last.Value;
			itemList.RemoveLast();
			itemList.AddFirst(item);

			currentItemNo--;

			var pos = itemScale * currentItemNo;
			item.anchoredPosition = (direction == Direction.Vertical) ? new Vector2(0, -pos): new Vector2(pos, 0);
			onUpdateItem.Invoke(currentItemNo, item.gameObject);

		}
	}

	private void AddTapEvent(GameObject item)
	{
    	// Unity 側で設定された Button コンポーネントを取得
    	Button button = item.GetComponent<Button>();
    	if (button == null)
    	{
        	Debug.LogError($"Button component is missing on {item.name}. Please add it in the prefab.");
        	return;
    	}

    	// 既存のリスナーをクリアして重複を防ぐ
    	button.onClick.RemoveAllListeners();

    	// タップ時の処理を設定
    	button.onClick.AddListener(() => OnItemTapped(item));
    	Debug.Log($"Tap event added to {item.name}");
	}

	private void OnItemTapped(GameObject item)
	{
    	Debug.Log($"Item tapped: {item.name}");
    	// 拡大表示の処理を呼び出す
    	ShowItemDetails(item);
	}

	[System.Serializable]
	public class OnItemPositionChange : UnityEngine.Events.UnityEvent<int, GameObject> {}
}