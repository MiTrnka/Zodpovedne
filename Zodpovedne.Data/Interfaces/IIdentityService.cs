using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zodpovedne.Data.Interfaces;

public interface IIdentityService
{
    Task InitializeRolesAndAdminAsync();
    // Další metody pro práci s identitou
}