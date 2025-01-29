namespace Zodpovedne.Data.Interfaces;

// Rozhraní pro inicializaci základních rolí a admin účtu, pod stejným rozhraním mohu případně do IoCDI zaregistrovat jinou implementaci, kdyby bylo potřeba
public interface IIdentityDataSeeder
{
    Task InitializeRolesAndAdminAsync();
}