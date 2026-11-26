using TheBarbarian.Scripts.Managers;
using TheBarbarian.Scripts.Managers.SceneController;
using VContainer;
using VContainer.Unity;

namespace TheBarbarian.Scripts.Scopes
{
    public class GameLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<SceneController>(Lifetime.Singleton);
        }
    }
}