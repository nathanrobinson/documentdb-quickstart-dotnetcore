using System;
using System.Linq;

namespace todo.Controllers
{
    using System.Net;
    using System.Threading.Tasks;
    //using System.Web.Mvc;
    //using Models;
    using Microsoft.AspNetCore.Mvc;
    using Models;

    public class ItemController : Controller
    {
        private readonly IDocumentRepository<Item> _repository;

        public ItemController(IDocumentRepository<Item> repository) {
            _repository = repository;
        }

        [ActionName("Index")]
        public async Task<IActionResult> Index()
        {
            var items = await _repository.RunQueryAsync((await _repository.QueryAsync())
                                                        .Where(d => !d.Completed));
            return View(items);
        }
        

#pragma warning disable 1998
        [ActionName("Create")]
        public async Task<IActionResult> CreateAsync()
        {
            return View();
        }
#pragma warning restore 1998

        [HttpPost]
        [ActionName("Create")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateAsync([Bind("Id,Name,Description,Completed")] Item item)
        {
            if (ModelState.IsValid)
            {
                await _repository.AddAsync(item);
                return RedirectToAction("Index");
            }

            return View(item);
        }

        [HttpPost]
        [ActionName("Edit")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> EditAsync([Bind("Id,Name,Description,Completed")] Item item)
        {
            if (ModelState.IsValid)
            {
                await _repository.UpdateAsync(item);
                return RedirectToAction("Index");
            }

            return View(item);
        }

        [ActionName("Edit")]
        public async Task<ActionResult> EditAsync(Guid id)
        {
            if (id == Guid.Empty)
            {
                return BadRequest();
            }

            Item item = await _repository.GetAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            return View(item);
        }

        [ActionName("Delete")]
        public async Task<ActionResult> DeleteAsync(Guid id)
        {
            if (id == Guid.Empty)
            {
                return BadRequest();
            }

            var item = await _repository.GetAsync(id);
            if (item == null)
            {
                return NotFound();
            }

            return View(item);
        }

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmedAsync([Bind("Id")] Guid id)
        {
            await _repository.DeleteAsync(id);
            return RedirectToAction("Index");
        }

        [ActionName("Details")]
        public async Task<ActionResult> DetailsAsync(Guid id)
        {
            Item item = await _repository.GetAsync(id);
            return View(item);
        }
    }
}