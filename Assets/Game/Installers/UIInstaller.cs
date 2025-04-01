using Assets.Game.UI.Config;
using Assets.Game.UI.Factory;
using UnityEngine;
using Zenject;

[CreateAssetMenu(fileName = "UIInstaller", menuName = "Installers/UIInstaller")]
public class UIInstaller : ScriptableObjectInstaller<UIInstaller>
{
    [Header("ScriptableObject Config")]
    [SerializeField] private UIConfig uiConfig;

    [Header("UI Prefabs")]
    [SerializeField] private GameObject buttonPrefab;
    [SerializeField] private GameObject labelPrefab;

    public override void InstallBindings()
    {
        // Bind the UIConfig as a singleton (or as needed).
        Container.Bind<UIConfig>().FromInstance(uiConfig).AsSingle();

        // Alternatively, you can do: Container.BindInstance(uiConfig).AsSingle();

        // Bind the prefabs by ID so our factory constructor can inject them properly
        Container.Bind<GameObject>().WithId("ButtonPrefab").FromInstance(buttonPrefab).AsTransient();
        Container.Bind<GameObject>().WithId("LabelPrefab").FromInstance(labelPrefab).AsTransient();

        // Bind the factory
        Container.Bind<IUIElementFactory>().To<UIElementFactory>().AsSingle();
    }
}