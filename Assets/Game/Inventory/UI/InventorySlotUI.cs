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
    public class InventorySlotUI : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image highlightImage;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color disabledColor = new Color(0.7f, 0.7f, 0.7f);

        private InventoryGridUI parentGrid;
        private Vector2Int gridPosition;
        private bool isHighlighted = false;
        private bool isValidDropTarget = true;

        public Vector2Int GridPosition => gridPosition;
        public InventoryGridUI ParentGrid => parentGrid;

        public void Initialize(InventoryGridUI parent, Vector2Int position)
        {
            parentGrid = parent;
            gridPosition = position;

            if (highlightImage != null)
                highlightImage.gameObject.SetActive(false);

            if (backgroundImage != null)
                backgroundImage.color = normalColor;
        }

        public void SetHighlight(bool highlight)
        {
            isHighlighted = highlight;
            if (highlightImage != null)
                highlightImage.gameObject.SetActive(highlight);
        }

        public void SetValidDropTarget(bool isValid)
        {
            isValidDropTarget = isValid;
            if (backgroundImage != null)
            {
                backgroundImage.color = isValid ? normalColor : disabledColor;
            }
        }

        // Implement IDropHandler
        public void OnDrop(PointerEventData eventData)
        {
            // Check if we have a dragged item
            InventoryItemUI draggedItem = InventoryUIManager.Instance.CurrentDraggedItem;
            if (draggedItem == null)
                return;

            // Notify the parent grid that an item has been dropped
            parentGrid.OnItemDroppedInSlot(this, draggedItem);
        }

        // Implement IPointerEnterHandler
        public void OnPointerEnter(PointerEventData eventData)
        {
            // If we're dragging an item, show whether this is a valid drop target
            InventoryItemUI draggedItem = InventoryUIManager.Instance.CurrentDraggedItem;
            if (draggedItem != null)
            {
                // Check with the parent grid if this is a valid position for the item
                bool canPlace = parentGrid.CanPlaceItemAt(
                    draggedItem.Item,
                    gridPosition,
                    draggedItem.SourceGrid != parentGrid  // Is this a cross-container operation?
                );

                // Highlight valid drop locations
                if (canPlace)
                {
                    // Additionally highlight all the cells this item would occupy
                    parentGrid.HighlightItemPlacement(gridPosition, draggedItem.Item.size);
                }
                else
                {
                    // Indicate this is not a valid drop target
                    SetValidDropTarget(false);
                }
            }
            else
            {
                // Regular hover highlight when not dragging
                SetHighlight(true);
            }
        }

        // Implement IPointerExitHandler
        public void OnPointerExit(PointerEventData eventData)
        {
            // Clear highlights and reset appearance
            SetHighlight(false);
            SetValidDropTarget(true);

            // Clear grid highlights if we were showing item placement
            InventoryItemUI draggedItem = InventoryUIManager.Instance.CurrentDraggedItem;
            if (draggedItem != null)
            {
                parentGrid.ClearAllHighlights();
            }
        }
    }
}
