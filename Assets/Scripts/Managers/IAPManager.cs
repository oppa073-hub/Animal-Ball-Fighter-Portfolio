using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.Purchasing;

public class IAPManager : MonoBehaviour
{
    public static IAPManager Instance { get; private set; }

    public const string Gold5000ProductId = "gold_5000";

    [Header("Gold Product")]
    [SerializeField] private int gold5000Amount = 5000;

    private StoreController storeController;
    private Product gold5000Product;

    public bool IsGold5000Ready => gold5000Product != null;
    public string Gold5000PriceText => gold5000Product != null
        ? gold5000Product.metadata.localizedPriceString
        : "불러오는 중...";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private async void Start()
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                await UnityServices.InitializeAsync();
            }

            storeController = UnityIAPServices.StoreController();

            storeController.OnStoreConnected += OnStoreConnected;
            storeController.OnStoreDisconnected += OnStoreDisconnected;
            storeController.OnProductsFetched += OnProductsFetched;
            storeController.OnProductsFetchFailed += OnProductsFetchFailed;
            storeController.OnPurchasesFetched += OnPurchasesFetched;
            storeController.OnPurchasesFetchFailed += OnPurchasesFetchFailed;
            storeController.OnPurchasePending += OnPurchasePending;
            storeController.OnPurchaseDeferred += OnPurchaseDeferred;
            storeController.OnPurchaseConfirmed += OnPurchaseConfirmed;
            storeController.OnPurchaseFailed += OnPurchaseFailed;

            Debug.Log("[IAP] 스토어 연결 요청");

            await storeController.Connect();

            var products = new List<ProductDefinition>
            {
                new ProductDefinition(
                    Gold5000ProductId,
                    ProductType.Consumable)
            };

            storeController.FetchProducts(products);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[IAP] 초기화 실패: {exception}");
        }
    }
    private void OnStoreConnected()
    {
        Debug.Log("[IAP] 스토어 연결 성공");
    }

    private void OnProductsFetched(List<Product> products)
    {
        gold5000Product = products.Find(product => product.definition.id == Gold5000ProductId);

        if (gold5000Product == null)
        {
            Debug.LogError("[IAP] gold_5000 상품을 찾지 못했습니다.");
            return;
        }

        Debug.Log(
            $"[IAP] 상품 로드 완료: {gold5000Product.definition.id} / " +
            $"{gold5000Product.metadata.localizedPriceString}");

        storeController.FetchPurchases();
    }

    private void OnProductsFetchFailed(ProductFetchFailed failure)
    {
        Debug.LogError($"[IAP] 상품 로드 실패: {failure}");
    }

    private void OnPurchasesFetched(Orders orders)
    {
        Debug.Log("[IAP] 기존 구매 내역 확인 완료");
    }

    private void OnPurchasesFetchFailed(
        PurchasesFetchFailureDescription failure)
    {
        Debug.LogError($"[IAP] 구매 내역 확인 실패: {failure}");
    }

    public void PurchaseGold5000()
    {
        if (gold5000Product == null)
        {
            Debug.LogWarning("[IAP] 상품이 아직 준비되지 않았습니다.");
            return;
        }

        Debug.Log("[IAP] gold_5000 구매 요청");

        storeController.PurchaseProduct(gold5000Product);
    }

    private void OnPurchasePending(PendingOrder order)
    {
        Product product =
            order.CartOrdered.Items().FirstOrDefault()?.Product;

        if (product == null)
        {
            Debug.LogError("[IAP] 구매 상품 정보를 찾지 못했습니다.");
            return;
        }

        if (product.definition.id != Gold5000ProductId)
        {
            Debug.LogError($"[IAP] 등록되지 않은 상품입니다: {product.definition.id}");

            return;
        }

        if (SaveManager.Instance == null)
        {
            Debug.LogError("[IAP] SaveManager가 없습니다.");
            return;
        }

        string transactionId = order.Info.TransactionID;

        if (string.IsNullOrWhiteSpace(transactionId))
        {
            Debug.LogError("[IAP] 구매 거래 ID를 찾지 못했습니다.");
            return;
        }

        bool granted = SaveManager.Instance.TryGrantIapGold(transactionId,gold5000Amount);

        if (granted)
        {
            Debug.Log(
                $"[IAP] 구매 보상 {gold5000Amount}G 지급 / " +
                $"거래: {transactionId}");

            ToastMessageManager.Instance?.Show("골드 5,000개 구매 완료");
        }
        else
        {
            Debug.LogWarning(
                $"[IAP] 이미 처리된 거래이므로 중복 지급하지 않습니다: " +
                $"{transactionId}");
        }

        // 신규 거래와 이미 지급된 거래 모두 승인
        storeController.ConfirmPurchase(order);
    }
    private void OnPurchaseDeferred(DeferredOrder order)
    {
        Debug.Log("[IAP] 구매 승인 대기 중");
    }

    private void OnPurchaseConfirmed(Order order)
    {
        Debug.Log("[IAP] 구매 처리 완료");
    }

    private void OnPurchaseFailed(FailedOrder order)
    {
        Debug.LogError($"[IAP] 구매 실패 또는 취소: {order}");
    }

    private void OnStoreDisconnected(
        StoreConnectionFailureDescription failure)
    {
        Debug.LogError($"[IAP] 스토어 연결 끊김: {failure}");
    }

    private void OnDestroy()
    {
        if (Instance != this) return;

        if (storeController != null)
        {
            storeController.OnStoreConnected -= OnStoreConnected;

            storeController.OnStoreDisconnected -= OnStoreDisconnected;

            storeController.OnProductsFetched -= OnProductsFetched;

            storeController.OnProductsFetchFailed -= OnProductsFetchFailed;

            storeController.OnPurchasesFetched -= OnPurchasesFetched;

            storeController.OnPurchasesFetchFailed -= OnPurchasesFetchFailed;

            storeController.OnPurchasePending -= OnPurchasePending;

            storeController.OnPurchaseDeferred -= OnPurchaseDeferred;

            storeController.OnPurchaseConfirmed -= OnPurchaseConfirmed;

            storeController.OnPurchaseFailed -= OnPurchaseFailed;
        }

        Instance = null;
    }
}