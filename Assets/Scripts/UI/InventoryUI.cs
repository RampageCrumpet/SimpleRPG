using UnityEngine;
using Inventory;
using UnityEngine.UI;

namespace SimpleRPG.UI
{
    [RequireComponent(typeof(GridLayoutGroup))]
    public class InventoryUI : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The backing inventory.")]
        public Inventory.Inventory inventory;

        /// <summary>
        /// The grid layout group governing control over this inventory.
        /// </summary>
        private GridLayoutGroup gridLayout;

        /// <summary>
        /// The prefab to be instantiated for every grid element.
        /// </summary>
        private GameObject UISquarePrefab;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            gridLayout = GetComponent<GridLayoutGroup>();
            if (gridLayout.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
            {
                gridLayout.constraintCount = inventory.inventorySize.x;
            }
            else if (gridLayout.constraint == GridLayoutGroup.Constraint.FixedRowCount)
            {
                gridLayout.constraintCount = inventory.inventorySize.y;
            }
            else
            {
                Debug.LogError(this.gameObject.name + " has an unrecognized GridLayoutGroup constraint.");
            }

            CreateBackingGrid();
        }

        // Update is called once per frame
        void Update()
        {

        }

        private void CreateBackingGrid()
        {
            for(int x = 0; x < inventory.inventorySize.x; x++)
            {
                for(int y = 0; y < inventory.inventorySize.y; y++) 
                {
                    GameObject newUISquarePrefab = Object.Instantiate(UISquarePrefab);
                    newUISquarePrefab.transform.parent = transform;
                }
            }
        }
    }
}