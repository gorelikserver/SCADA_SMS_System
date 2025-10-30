/**
 * SCADA SMS System - Enhanced UI Functionality
 * Main JavaScript utilities for improved user experience
 */

// Global utilities
window.SCADAUtils = {
    // Debounce function for search inputs
    debounce: function(func, wait) {
        let timeout;
        return function executedFunction(...args) {
            const later = () => {
                clearTimeout(timeout);
                func(...args);
            };
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
        };
    },

    // Format date for display
    formatDate: function(date) {
        if (!date) return 'N/A';
        return new Date(date).toLocaleDateString();
    },

    // Format time for display
    formatTime: function(date) {
        if (!date) return 'N/A';
        return new Date(date).toLocaleTimeString();
    },

    // Show loading state
    showLoading: function(element) {
        element.classList.add('loading');
        element.disabled = true;
   ,

    // Hide loading state
    hideLoading: function(element) {
        element.classList.remove('loading');
        element.disabled = false;
    },

    // Show toast notification
    showToast: function(message, type = 'info') {
        // Create toast element if it doesn't exist
        let toastContainer = document.getElementById('toast-container');
        if (!toastContainer) {
            toastContainer = document.createElement('div');
            toastContainer.id = 'toast-container';
            toastContainer.className = 'position-fixed top-0 end-0 p-3';
            toastContainer.style.zIndex = '9999';
            document.body.appendChild(toastContainer);
        }

        const toastId = 'toast-' + Date.now();
        const toastHTML = `
            <div id="${toastId}" class="toast align-items-center text-bg-${type} border-0" role="alert" aria-live="assertive" aria-atomic="true">
                <div class="d-flex">
                    <div class="toast-body">
                        ${message}
                    </div>
                    <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast" aria-label="Close"></button>
                </div>
            </div>
        `;

        toastContainer.insertAdjacentHTML('beforeend', toastHTML);
        const toastElement = document.getElementById(toastId);
        const toast = new bootstrap.Toast(toastElement, { delay: 3000 });
        toast.show();

        // Remove toast element after it's hidden
        toastElement.addEventListener('hidden.bs.toast', () => {
            toastElement.remove();
        });
    },

    // Validate form fields
    validateForm: function(form) {
        const requiredFields = form.querySelectorAll('[required]');
        let isValid = true;

        requiredFields.forEach(field => {
            if (!field.value.trim()) {
                field.classList.add('is-invalid');
                isValid = false;
            } else {
                field.classList.remove('is-invalid');
                field.classList.add('is-valid');
            }
        });

        return isValid;
    },

    // Confirmation dialog helper
    confirmAction: function(message, callback) {
        if (confirm(message)) {
            callback();
        }
    },

    // Format phone number helper
    formatPhoneNumber: function(input) {
        var phoneNumber = input.value.replace(/\D/g, '');

        if (phoneNumber.length > 0) {
            if (phoneNumber.length > 10) {
                // Format with country code
                phoneNumber = '+' + phoneNumber.substring(0, 1) + ' '
                    + phoneNumber.substring(1, 4) + '-'
                    + phoneNumber.substring(4, 7) + '-'
                    + phoneNumber.substring(7, 11);
            } else if (phoneNumber.length > 6) {
                // Format without country code
                phoneNumber = phoneNumber.substring(0, 3) + '-'
                    + phoneNumber.substring(3, 6) + '-'
                    + phoneNumber.substring(6, 10);
            } else if (phoneNumber.length > 3) {
                // Partial formatting
                phoneNumber = phoneNumber.substring(0, 3) + '-'
                    + phoneNumber.substring(3);
            }
        }

        input.value = phoneNumber;
    },

    // Sidebar toggle for mobile
    toggleSidebar: function() {
        const sidebar = document.querySelector('.sidebar');
        if (sidebar) {
            sidebar.classList.toggle('active');
        }
    },

    // Copy text to clipboard
    copyToClipboard: function(text) {
        if (navigator.clipboard) {
            navigator.clipboard.writeText(text).then(() => {
                this.showToast('Copied to clipboard!', 'success');
            }).catch(() => {
                this.showToast('Failed to copy to clipboard', 'danger');
            });
        } else {
            // Fallback for older browsers
            const textArea = document.createElement('textarea');
            textArea.value = text;
            document.body.appendChild(textArea);
            textArea.select();
            try {
                document.execCommand('copy');
                this.showToast('Copied to clipboard!', 'success');
            } catch (err) {
                this.showToast('Failed to copy to clipboard', 'danger');
            }
            document.body.removeChild(textArea);
        }
    },

    // Auto-resize textarea
    autoResizeTextarea: function(textarea) {
        textarea.style.height = 'auto';
        textarea.style.height = textarea.scrollHeight + 'px';
    },

    // Highlight search terms
    highlightSearchTerms: function(text, searchTerm) {
        if (!searchTerm.trim()) return text;
        
        const regex = new RegExp(`(${searchTerm.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')})`, 'gi');
        return text.replace(regex, '<span class="search-highlight">$1</span>');
    }
};

// Base Table Filter Class
class BaseTableFilter {
    constructor(tableId) {
        this.table = document.getElementById(tableId);
        if (!this.table) {
            console.error(`Table with ID '${tableId}' not found`);
            return;
        }
        
        this.rows = Array.from(this.table.querySelectorAll('tbody tr'));
        this.totalRows = this.rows.length;
        this.sortDirection = {};
        this.activeFilters = {};
        
        this.init();
    }
    
    init() {
        this.setupEventListeners();
        this.updateCounts();
        this.initTooltips();
    }
    
    initTooltips() {
        const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
        tooltipTriggerList.map(function(tooltipTriggerEl) {
            return new bootstrap.Tooltip(tooltipTriggerEl);
        });
    }
    
    setupEventListeners() {
        // Quick search with debouncing
        const quickSearchInput = document.getElementById('quickSearch');
        if (quickSearchInput) {
            const debouncedSearch = SCADAUtils.debounce((value) => {
                this.quickSearch(value);
            }, 300);
            
            quickSearchInput.addEventListener('input', (e) => {
                debouncedSearch(e.target.value);
            });
        }
        
        // Clear quick search
        const clearQuickSearchBtn = document.getElementById('clearQuickSearch');
        if (clearQuickSearchBtn) {
            clearQuickSearchBtn.addEventListener('click', () => {
                if (quickSearchInput) {
                    quickSearchInput.value = '';
                    this.quickSearch('');
                }
            });
        }
        
        // Column filters with debouncing
        document.querySelectorAll('.column-filter').forEach(filter => {
            const debouncedFilter = SCADAUtils.debounce(() => {
                this.applyFilters();
            }, 300);
            
            filter.addEventListener('input', debouncedFilter);
            filter.addEventListener('change', debouncedFilter);
        });
        
        // Sort buttons
        document.querySelectorAll('.sort-btn').forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.preventDefault();
                const column = parseInt(e.currentTarget.dataset.column);
                this.sortTable(column);
            });
        });
        
        // Filter toggle
        const toggleFiltersBtn = document.getElementById('toggleFilters');
        if (toggleFiltersBtn) {
            toggleFiltersBtn.addEventListener('click', () => {
                this.toggleFilterSection();
            });
        }
        
        // Clear all filters
        const clearAllFiltersBtn = document.getElementById('clearAllFilters');
        if (clearAllFiltersBtn) {
            clearAllFiltersBtn.addEventListener('click', () => {
                this.clearAllFilters();
            });
        }
        
        // Export functionality
        const exportBtn = document.getElementById('exportVisible');
        if (exportBtn) {
            exportBtn.addEventListener('click', () => {
                this.exportVisible();
            });
        }
        
        // Refresh functionality
        const refreshBtn = document.getElementById('refreshTable');
        if (refreshBtn) {
            refreshBtn.addEventListener('click', () => {
                location.reload();
            });
        }
    }
    
    quickSearch(searchTerm) {
        const term = searchTerm.toLowerCase().trim();
        
        this.rows.forEach(row => {
            if (term === '') {
                row.style.display = '';
                // Remove any existing highlights
                row.innerHTML = row.innerHTML.replace(/<span class="search-highlight">(.*?)<\/span>/gi, '$1');
            } else {
                const text = row.textContent.toLowerCase();
                if (text.includes(term)) {
                    row.style.display = '';
                    // Highlight search terms (basic implementation)
                    // Note: This is a simplified version, full implementation would be more complex
                } else {
                    row.style.display = 'none';
                }
            }
        });
        
        this.updateCounts();
    }
    
    applyFilters() {
        // To be implemented by child classes
        this.updateCounts();
    }
    
    sortTable(columnIndex) {
        const currentDirection = this.sortDirection[columnIndex] || 'asc';
        const newDirection = currentDirection === 'asc' ? 'desc' : 'asc';
        this.sortDirection[columnIndex] = newDirection;
        
        // Update sort icons
        document.querySelectorAll('.sort-btn i').forEach(icon => {
            icon.className = 'fas fa-sort text-muted';
        });
        
        const currentIcon = document.querySelector(`[data-column="${columnIndex}"] i`);
        if (currentIcon) {
            currentIcon.className = `fas fa-sort-${newDirection === 'asc' ? 'up' : 'down'} text-primary`;
        }
        
        // Sort implementation - to be customized by child classes
        this.performSort(columnIndex, newDirection);
    }
    
    performSort(columnIndex, direction) {
        // Basic text sorting - override in child classes for custom sorting
        const sortedRows = this.rows.sort((a, b) => {
            const aText = a.cells[columnIndex]?.textContent?.trim() || '';
            const bText = b.cells[columnIndex]?.textContent?.trim() || '';
            
            let comparison = aText.localeCompare(bText);
            return direction === 'asc' ? comparison : -comparison;
        });
        
        // Reorder DOM
        const tbody = this.table.querySelector('tbody');
        sortedRows.forEach(row => tbody.appendChild(row));
        
        this.rows = sortedRows;
    }
    
    toggleFilterSection() {
        const section = document.getElementById('filterSection');
        const icon = document.getElementById('filterToggleIcon');
        
        if (section && icon) {
            if (section.style.display === 'none') {
                section.style.display = 'block';
                icon.className = 'fas fa-chevron-down';
            } else {
                section.style.display = 'none';
                icon.className = 'fas fa-chevron-right';
            }
        }
    }
    
    clearAllFilters() {
        // Clear all filter inputs
        document.querySelectorAll('.column-filter').forEach(filter => {
            filter.value = '';
        });
        
        const quickSearchInput = document.getElementById('quickSearch');
        if (quickSearchInput) {
            quickSearchInput.value = '';
        }
        
        // Clear active filters
        this.activeFilters = {};
        
        // Show all rows
        this.rows.forEach(row => {
            row.style.display = '';
        });
        
        // Reset sort icons
        document.querySelectorAll('.sort-btn i').forEach(icon => {
            icon.className = 'fas fa-sort text-muted';
        });
        this.sortDirection = {};
        
        this.updateCounts();
        
        SCADAUtils.showToast('All filters cleared', 'info');
    }
    
    updateCounts() {
        const visibleRows = this.rows.filter(row => row.style.display !== 'none');
        
        // Update count elements if they exist
        const visibleCountEl = document.getElementById('visibleCount');
        const totalCountEl = document.getElementById('totalCount');
        const resultsCountEl = document.getElementById('resultsCount');
        const lastUpdatedEl = document.getElementById('lastUpdated');
        
        if (visibleCountEl) visibleCountEl.textContent = visibleRows.length;
        if (totalCountEl) totalCountEl.textContent = this.totalRows;
        if (resultsCountEl) resultsCountEl.textContent = visibleRows.length;
        
        if (lastUpdatedEl) {
            const now = new Date();
            lastUpdatedEl.textContent = now.toLocaleTimeString();
        }
    }
    
    exportVisible() {
        const visibleRows = this.rows.filter(row => row.style.display !== 'none');
        
        if (visibleRows.length === 0) {
            SCADAUtils.showToast('No data to export', 'warning');
            return;
        }
        
        const csv = [];
        
        // Headers
        const headers = Array.from(this.table.querySelectorAll('thead th')).map(th => {
            return th.textContent.replace(/[^\w\s]/gi, '').trim();
        });
        csv.push(headers.map(h => `"${h}"`).join(','));
        
        // Data rows
        visibleRows.forEach(row => {
            const cells = Array.from(row.querySelectorAll('td')).map(cell => {
                // Clean cell text
                let text = cell.textContent.trim();
                // Remove button text, keep only data
                if (cell.querySelector('button')) {
                    text = cell.textContent.replace(/[^\w\s@.-]/gi, '').trim();
                }
                return `"${text.replace(/"/g, '""')}"`;
            });
            csv.push(cells.join(','));
        });
        
        // Download
        this.downloadCSV(csv.join('\n'), `export_${new Date().toISOString().split('T')[0]}.csv`);
        
        SCADAUtils.showToast(`Exported ${visibleRows.length} records`, 'success');
    }
    
    downloadCSV(csvContent, filename) {
        const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
        const link = document.createElement('a');
        
        if (link.download !== undefined) {
            const url = URL.createObjectURL(blob);
            link.setAttribute('href', url);
            link.setAttribute('download', filename);
            link.style.visibility = 'hidden';
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            URL.revokeObjectURL(url);
        }
    }
}

// Initialize common functionality on page load
document.addEventListener('DOMContentLoaded', function() {
    // Initialize tooltips globally
    const tooltipTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="tooltip"]'));
    tooltipTriggerList.map(function(tooltipTriggerEl) {
        return new bootstrap.Tooltip(tooltipTriggerEl);
    });
    
    // Auto-resize textareas
    document.querySelectorAll('textarea[data-auto-resize]').forEach(textarea => {
        textarea.addEventListener('input', () => {
            SCADAUtils.autoResizeTextarea(textarea);
        });
        // Initial resize
        SCADAUtils.autoResizeTextarea(textarea);
    });
    
    // Phone number formatting
    document.querySelectorAll('input[type="tel"], input[data-phone]').forEach(input => {
        input.addEventListener('input', () => {
            SCADAUtils.formatPhoneNumber(input);
        });
    });
    
    // Form validation on submit
    document.querySelectorAll('form[data-validate]').forEach(form => {
        form.addEventListener('submit', (e) => {
            if (!SCADAUtils.validateForm(form)) {
                e.preventDefault();
                SCADAUtils.showToast('Please fill in all required fields', 'danger');
            }
        });
    });
    
    // Copy to clipboard functionality
    document.querySelectorAll('[data-copy]').forEach(element => {
        element.addEventListener('click', () => {
            const textToCopy = element.dataset.copy || element.textContent;
            SCADAUtils.copyToClipboard(textToCopy);
        });
    });
    
    // Auto-dismiss alerts after 5 seconds
    document.querySelectorAll('.alert:not(.alert-permanent)').forEach(alert => {
        setTimeout(() => {
            if (alert.parentNode) {
                alert.style.transition = 'opacity 0.5s ease';
                alert.style.opacity = '0';
                setTimeout(() => {
                    if (alert.parentNode) {
                        alert.remove();
                    }
                }, 500);
            }
        }, 5000);
    });
});

// Global error handler
window.addEventListener('error', function(e) {
    console.error('Global error:', e.error);
    // Could implement error reporting here
});

// Export for global use
window.BaseTableFilter = BaseTableFilter;