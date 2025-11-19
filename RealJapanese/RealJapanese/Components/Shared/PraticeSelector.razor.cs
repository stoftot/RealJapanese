using Microsoft.AspNetCore.Components;

namespace RealJapanese.Components.Shared;

public partial class PraticeSelectorBase : ComponentBase
{
    [Parameter] public string MainUrl { get; set; } = "";
    [Parameter] public List<PageDescreption> Pages { get; set; } = [];
    [Inject] private NavigationManager NavigationManager { get; set; } = null!;

    protected void GoToUrl(PageDescreption page)
    {
        NavigationManager.NavigateTo($"{MainUrl}/{page.Url}");
    }
    
    public class PageDescreption
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Url { get; set; } = "";
    }
}