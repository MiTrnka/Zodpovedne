using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zodpovedne.Contracts.DTO;

public class KatalogDetailDto
{
    public KatalogDetailDto(string Name, string Description, string ImageUrl, decimal Price, string Url)
    {
        this.Name = Name;
        this.Description = Description;
        this.ImageUrl = ImageUrl;
        this.Price = Price;
        this.Url = Url;
    }

    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public decimal Price { get; set; } = 0;
    public string Url { get; set; } = "";
}
