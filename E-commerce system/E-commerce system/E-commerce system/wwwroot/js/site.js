// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Global function to add product to wishlist
async function addToWishlist(productId) {
    try {
        const response = await fetch('/Wishlist/AddToWishlist?productId=' + productId, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            }
        });
        const data = await response.json();
        
        if (data.success) {
            alert(data.message);
        } else {
            alert(data.message);
            if (data.message.includes('login')) {
                window.location.href = '/Account/Login';
            }
        }
    } catch (error) {
        console.error('Error:', error);
        alert('Something went wrong. Please try again.');
    }
}
