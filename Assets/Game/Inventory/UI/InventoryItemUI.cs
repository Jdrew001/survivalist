using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.EventSystems;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Game.Inventory.UI
{
    public class InventoryItemUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Text quantityText;
        [SerializeField] private CanvasGroup canvasGroup;

        private InventoryItem item;
        private Vector2Int gridPosition;
        private InventoryGridUI sourceGrid;
        private RectTransform rectTransform;
        private Transform originalParent;
        private Vector3 originalPosition;
        private Vector2 originalSizeDelta;

        public InventoryItem Item => item;
        public Vector2Int GridPosition => gridPosition;
        public InventoryGridUI SourceGrid => sourceGrid;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();

            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public void Setup(InventoryItem item, Vector2Int position, InventoryGridUI parentGrid)
        {
            this.item = item;
            this.gridPosition = position;
            this.sourceGrid = parentGrid;

            UpdateVisual();
        }

        public void UpdateVisual()
        {
            // Update the icon
            if (iconImage != null && item.icon != null)
            {
                iconImage.sprite = item.icon;
                iconImage.preserveAspect = true;
            }

            // Update quantity text
            if (quantityText != null)
            {
                if (item.isStackable && item.currentStackSize > 1)
                {
                    quantityText.text = item.currentStackSize.ToString();
                    quantityText.gameObject.SetActive(true);
                }
                else
                {
                    quantityText.gameObject.SetActive(false);
                }
            }

            // Update size for multi-cell items
            if (item.size.x > 1 || item.size.y > 1)
            {
                // Calculate size based on grid cell size and spacing
                Vector2 cellSize = sourceGrid.CellSize;
                Vector2 spacing = sourceGrid.Spacing;

                rectTransform.sizeDelta = new Vector2(
                    item.size.x * cellSize.x + (item.size.x - 1) * spacing.x,
                    item.size.y * cellSize.y + (item.size.y - 1) * spacing.y
                );
            }
        }

        #region Drag and Drop Handlers

        public void OnBeginDrag(PointerEventData eventData)
        {
            // Store original values
            originalParent = transform.parent;
            originalPosition = transform.position;
            originalSizeDelta = rectTransform.sizeDelta;

            // Make the item appear above other UI elements while dragging
            transform.SetParent(InventoryUIManager.Instance.DragLayer);

            // Make semi-transparent while dragging
            canvasGroup.alpha = 0.8f;
            canvasGroup.blocksRaycasts = false;

            // Notify UI manager that we're being dragged
            InventoryUIManager.Instance.OnItemBeginDrag(this);
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Update position to follow cursor
            transform.position = eventData.position;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            // Reset appearance
            canvasGroup.alpha = 1.0f;
            canvasGroup.blocksRaycasts = true;

            // Notify UI manager drag has ended
            InventoryUIManager.Instance.OnItemEndDrag(this);
        }

        #endregion

        public void ReturnToOriginalPosition()
        {
            transform.SetParent(originalParent);
            transform.position = originalPosition;
            rectTransform.sizeDelta = originalSizeDelta;
        }
    }
}
