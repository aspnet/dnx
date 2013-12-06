using Owin;

namespace HelloWeb
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            System.Console.WriteLine("Web starting!");
            app.UseWelcomePage();
        }
    }
}
