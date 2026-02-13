// Admin Dashboard JavaScript Functions

// Ensure functions are in global scope
window.viewUser = viewUser;
window.viewDevice = viewDevice;
window.viewAlert = viewAlert;
window.deleteUser = deleteUser;
window.deleteDevice = deleteDevice;
window.deleteAlert = deleteAlert;

console.log('site.js loaded - Admin functions available');

// View User Details
function viewUser(userKey) {
    if (!userKey) {
        console.error('User key is required');
        return;
    }
    
    // Redirect to user details page (you can create this page later)
    window.location.href = `/Users/Details?key=${encodeURIComponent(userKey)}`;
}

// View Device Details
function viewDevice(deviceKey) {
    if (!deviceKey) {
        console.error('Device key is required');
        return;
    }
    
    // Redirect to device details page
    window.location.href = `/Devices/Details?key=${encodeURIComponent(deviceKey)}`;
}

// View Alert Details
function viewAlert(alertKey) {
    if (!alertKey) {
        console.error('Alert key is required');
        return;
    }
    
    // Redirect to alert details page
    window.location.href = `/DeviceAlerts/Details?key=${encodeURIComponent(alertKey)}`;
}

// Delete User (with confirmation)
function deleteUser(userKey, userName) {
    if (!userKey) {
        console.error('User key is required');
        return;
    }
    
    const message = userName 
        ? `Are you sure you want to delete user "${userName}"?`
        : 'Are you sure you want to delete this user?';
    
    if (confirm(message)) {
        // Submit delete form
        const form = document.createElement('form');
        form.method = 'POST';
        form.action = `/Users/Index?handler=Delete&key=${encodeURIComponent(userKey)}`;
        
        // Add anti-forgery token
        const token = document.querySelector('input[name="__RequestVerificationToken"]');
        if (token) {
            const tokenInput = document.createElement('input');
            tokenInput.type = 'hidden';
            tokenInput.name = '__RequestVerificationToken';
            tokenInput.value = token.value;
            form.appendChild(tokenInput);
        }
        
        document.body.appendChild(form);
        form.submit();
    }
}

// Delete Device (with confirmation)
function deleteDevice(deviceKey, deviceName) {
    if (!deviceKey) {
        console.error('Device key is required');
        return;
    }
    
    const message = deviceName 
        ? `Are you sure you want to delete device "${deviceName}"?`
        : 'Are you sure you want to delete this device?';
    
    if (confirm(message)) {
        const form = document.createElement('form');
        form.method = 'POST';
        form.action = `/Devices/Index?handler=Delete&key=${encodeURIComponent(deviceKey)}`;
        
        const token = document.querySelector('input[name="__RequestVerificationToken"]');
        if (token) {
            const tokenInput = document.createElement('input');
            tokenInput.type = 'hidden';
            tokenInput.name = '__RequestVerificationToken';
            tokenInput.value = token.value;
            form.appendChild(tokenInput);
        }
        
        document.body.appendChild(form);
        form.submit();
    }
}

// Delete Alert (with confirmation)
function deleteAlert(alertKey) {
    if (!alertKey) {
        console.error('Alert key is required');
        return;
    }
    
    if (confirm('Are you sure you want to delete this alert?')) {
        const form = document.createElement('form');
        form.method = 'POST';
        form.action = `/DeviceAlerts/Index?handler=Delete&key=${encodeURIComponent(alertKey)}`;
        
        const token = document.querySelector('input[name="__RequestVerificationToken"]');
        if (token) {
            const tokenInput = document.createElement('input');
            tokenInput.type = 'hidden';
            tokenInput.name = '__RequestVerificationToken';
            tokenInput.value = token.value;
            form.appendChild(tokenInput);
        }
        
        document.body.appendChild(form);
        form.submit();
    }
}

// Show loading spinner
function showLoading(elementId) {
    const element = document.getElementById(elementId);
    if (element) {
        element.innerHTML = '<div class="loading-spinner"></div> Loading...';
    }
}

// Hide loading spinner
function hideLoading(elementId) {
    const element = document.getElementById(elementId);
    if (element) {
        element.innerHTML = '';
    }
}

// Format date/time
function formatDateTime(dateString) {
    const date = new Date(dateString);
    return date.toLocaleString();
}

// Format relative time (e.g., "2 hours ago")
function formatRelativeTime(dateString) {
    const date = new Date(dateString);
    const now = new Date();
    const diffMs = now - date;
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMins / 60);
    const diffDays = Math.floor(diffHours / 24);
    
    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return `${diffMins} minute${diffMins > 1 ? 's' : ''} ago`;
    if (diffHours < 24) return `${diffHours} hour${diffHours > 1 ? 's' : ''} ago`;
    return `${diffDays} day${diffDays > 1 ? 's' : ''} ago`;
}

// Initialize tooltips (Bootstrap)
document.addEventListener('DOMContentLoaded', function() {
    // Initialize Bootstrap tooltips if available
    if (typeof bootstrap !== 'undefined' && bootstrap.Tooltip) {
        const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
        tooltipTriggerList.map(function (tooltipTriggerEl) {
            return new bootstrap.Tooltip(tooltipTriggerEl);
        });
    }
    
    // Add fade-in animation to main content
    const mainContent = document.querySelector('main');
    if (mainContent) {
        mainContent.classList.add('fade-in');
    }
});

// Auto-dismiss alerts after 5 seconds
document.addEventListener('DOMContentLoaded', function() {
    const alerts = document.querySelectorAll('.alert:not(.alert-permanent)');
    alerts.forEach(function(alert) {
        setTimeout(function() {
            const bsAlert = new bootstrap.Alert(alert);
            bsAlert.close();
        }, 5000);
    });
});

// Copy to clipboard function
function copyToClipboard(text, buttonElement) {
    navigator.clipboard.writeText(text).then(function() {
        // Show success feedback
        const originalText = buttonElement.innerHTML;
        buttonElement.innerHTML = '<i class="fas fa-check"></i> Copied!';
        buttonElement.classList.add('btn-success');
        
        setTimeout(function() {
            buttonElement.innerHTML = originalText;
            buttonElement.classList.remove('btn-success');
        }, 2000);
    }).catch(function(err) {
        console.error('Failed to copy:', err);
        alert('Failed to copy to clipboard');
    });
}

// Export table to CSV
function exportTableToCSV(tableId, filename) {
    const table = document.getElementById(tableId);
    if (!table) {
        console.error('Table not found:', tableId);
        return;
    }
    
    let csv = [];
    const rows = table.querySelectorAll('tr');
    
    for (let i = 0; i < rows.length; i++) {
        const row = [];
        const cols = rows[i].querySelectorAll('td, th');
        
        for (let j = 0; j < cols.length; j++) {
            // Skip action columns
            if (cols[j].classList.contains('table-actions')) continue;
            
            let text = cols[j].innerText;
            row.push('"' + text.replace(/"/g, '""') + '"');
        }
        
        csv.push(row.join(','));
    }
    
    // Download CSV
    const csvFile = new Blob([csv.join('\n')], { type: 'text/csv' });
    const downloadLink = document.createElement('a');
    downloadLink.download = filename || 'export.csv';
    downloadLink.href = window.URL.createObjectURL(csvFile);
    downloadLink.style.display = 'none';
    document.body.appendChild(downloadLink);
    downloadLink.click();
    document.body.removeChild(downloadLink);
}

// Search/filter table
function filterTable(inputId, tableId) {
    const input = document.getElementById(inputId);
    const table = document.getElementById(tableId);
    
    if (!input || !table) return;
    
    const filter = input.value.toLowerCase();
    const rows = table.getElementsByTagName('tr');
    
    for (let i = 1; i < rows.length; i++) { // Skip header row
        const cells = rows[i].getElementsByTagName('td');
        let found = false;
        
        for (let j = 0; j < cells.length; j++) {
            if (cells[j].innerText.toLowerCase().indexOf(filter) > -1) {
                found = true;
                break;
            }
        }
        
        rows[i].style.display = found ? '' : 'none';
    }
}
