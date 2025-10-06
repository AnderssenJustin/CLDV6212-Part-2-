using Microsoft.AspNetCore.Mvc;
using ABCRetailers.Models;
using ABCRetailers.Models.ViewModels;
using ABCRetailers.Services;

namespace ABCRetailers.Controllers
{
    public class OrderController : Controller
    {
        private readonly IFunctionsApi _api;
        public OrderController(IFunctionsApi api) => _api = api;

        // LIST
        public async Task<IActionResult> Index(string search)
        {
            var orders = await _api.GetOrdersAsync();
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                orders = orders.Where(o =>
                    (o.Id?.ToLower().Contains(search) ?? false) ||
                    (o.CustomerId?.ToLower().Contains(search) ?? false) ||
                    (o.ProductId?.ToLower().Contains(search) ?? false) ||
                    (o.ProductName?.ToLower().Contains(search) ?? false) ||
                    (o.Status.ToString().ToLower().Contains(search)) ||
                    (o.Quantity.ToString().Contains(search)) ||
                    (o.TotalAmount.ToString("C").ToLower().Contains(search)) ||
                    (o.OrderDateUtc.HasValue && o.OrderDateUtc.Value.ToString("yyyy-MM-dd").Contains(search))
                ).ToList();
            }

            // Pass search term to view
            ViewBag.SearchTerm = search;
            return View(orders.OrderByDescending(o => o.OrderDateUtc).ToList());
        }

        // CREATE (GET)
        public async Task<IActionResult> Create()
        {
            var customers = await _api.GetCustomersAsync();
            var products = await _api.GetProductsAsync();

            var vm = new OrderCreateViewModel
            {
                Customers = customers,
                Products = products
            };
            return View(vm);
        }

        // CREATE (POST)
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OrderCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdowns(model);
                return View(model);
            }

            try
            {
                // Validate references (optional; Functions can also validate)
                var customer = await _api.GetCustomerAsync(model.CustomerId);
                var product = await _api.GetProductAsync(model.ProductId);

                if (customer is null || product is null)
                {
                    ModelState.AddModelError(string.Empty, "Invalid customer or product selected.");
                    await PopulateDropdowns(model);
                    return View(model);
                }

                if (product.StockAvailable < model.Quantity)
                {
                    ModelState.AddModelError("Quantity", $"Insufficient stock. Available: {product.StockAvailable}");
                    await PopulateDropdowns(model);
                    return View(model);
                }

                // Create order via Function (Function will set UTC time, snapshot price, update stock, enqueue messages)
                var saved = await _api.CreateOrderAsync(model.CustomerId, model.ProductId, model.Quantity);

                TempData["Success"] = "Order created successfully!";
                return RedirectToAction(nameof(Index));
                
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Error creating order: {ex.Message}");
                await PopulateDropdowns(model);
                return View(model);
            }
        }

        // DETAILS
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();
            var order = await _api.GetOrderAsync(id);
            return order is null ? NotFound() : View(order);
        }

        // EDIT (GET) - typically only status is editable
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return NotFound();
            var order = await _api.GetOrderAsync(id);
            return order is null ? NotFound() : View(order);
        }

        // EDIT (POST) - status only
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Order posted)
        {
            if (!ModelState.IsValid) return View(posted);

            try
            {
                await _api.UpdateOrderStatusAsync(posted.Id, posted.Status.ToString());
                TempData["Success"] = "Order updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, $"Error updating order: {ex.Message}");
                return View(posted);
            }
        }

        // DELETE
        [HttpPost]
        public async Task<IActionResult> Delete(string id)
        {
            try
            {
                await _api.DeleteOrderAsync(id);
                TempData["Success"] = "Order deleted successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting order: {ex.Message}";
            }
            return RedirectToAction(nameof(Index));
        }

        // AJAX: price/stock lookup
        [HttpGet]
        public async Task<JsonResult> GetProductPrice(string productId)
        {
            try
            {
                var product = await _api.GetProductAsync(productId);
                if (product is not null)
                {
                    return Json(new
                    {
                        success = true,
                        price = product.Price,
                        stock = product.StockAvailable,
                        productName = product.ProductName
                    });
                }
                return Json(new { success = false });
            }
            catch
            {
                return Json(new { success = false });
            }
        }

        // AJAX: status update
        [HttpPost]
        public async Task<IActionResult> UpdateOrderStatus(string id, string newStatus)
        {
            try
            {
                await _api.UpdateOrderStatusAsync(id, newStatus);
                return Json(new { success = true, message = $"Order status updated to {newStatus}" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        private async Task PopulateDropdowns(OrderCreateViewModel model)
        {
            model.Customers = await _api.GetCustomersAsync();
            model.Products = await _api.GetProductsAsync();
        }
    }
}
