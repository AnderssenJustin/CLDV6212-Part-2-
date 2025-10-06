using Microsoft.AspNetCore.Mvc;
using ABCRetailers.Models;
using ABCRetailers.Services;

namespace ABCRetailers.Controllers
{
    public class CustomerController : Controller
    {
        private readonly IFunctionsApi _api;
        public CustomerController(IFunctionsApi api) => _api = api;

        public async Task<IActionResult> Index(string search )
        {
            var customers = await _api.GetCustomersAsync();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                customers = customers.Where(c =>
                    (c.Name?.ToLower().Contains(search) ?? false) ||
                    (c.Surname?.ToLower().Contains(search) ?? false) ||
                    (c.Username?.ToLower().Contains(search) ?? false) ||
                    (c.Email?.ToLower().Contains(search) ?? false) ||
                    (c.ShippingAddress?.ToLower().Contains(search) ?? false) ||
                    (c.Id?.ToLower().Contains(search) ?? false)
                ).ToList();
            }

            // Pass search term to view to maintain it in the search box
            ViewBag.SearchTerm = search;
            return View(customers);
        }

        public IActionResult Create() => View();

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer customer)
        {
            if (!ModelState.IsValid) return View(customer);
            try
            {
                await _api.CreateCustomerAsync(customer);
                TempData["Success"] = "Customer created successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error creating customer: {ex.Message}");
                return View(customer);
            }
        }

        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();
            var customer = await _api.GetCustomerAsync(id);
            return customer is null ? NotFound() : View(customer);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Customer customer)
        {
            if (!ModelState.IsValid) return View(customer);
            try
            {
                await _api.UpdateCustomerAsync(customer.Id, customer);
                TempData["Success"] = "Customer updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Error updating customer: {ex.Message}");
                return View(customer);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                await _api.DeleteCustomerAsync(id);
                TempData["Success"] = "Customer deleted successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting customer: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
