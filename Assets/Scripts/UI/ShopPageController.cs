using UnityEngine;

public class ShopPageController : MonoBehaviour
{
    [SerializeField] private ShopItemData[] shopItems;
    [SerializeField] private ShopItemCardUI itemCardPrefab;
    [SerializeField] private CharacterLevelShopUI characterLevelCardPrefab;
    [SerializeField] private Transform contentRoot;

    private void Start()
    {
        CreateShopItems();
    }

    private void CreateShopItems()
    {
        CharacterLevelShopUI characterCard = Instantiate(characterLevelCardPrefab, contentRoot);

        characterCard.Refresh();

        for (int i = 0; i < shopItems.Length; i++)
        {
            ShopItemCardUI card = Instantiate(itemCardPrefab, contentRoot);

            card.Initialize(shopItems[i]);
        }
    }
}