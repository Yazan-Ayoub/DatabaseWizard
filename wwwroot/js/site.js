// Database Wizard - Client-side logic
let tableIdCounter = 0;
let relationIdCounter = 0;

document.addEventListener('DOMContentLoaded', function() {
    initializeEventListeners();
    // Add initial table
    addTable();
});

function initializeEventListeners() {
    document.getElementById('addTableBtn').addEventListener('click', addTable);
    document.getElementById('addRelationBtn').addEventListener('click', addRelation);
    document.getElementById('previewBtn').addEventListener('click', previewSql);
    document.getElementById('databaseForm').addEventListener('submit', handleSubmit);
}

function addTable() {
    const template = document.getElementById('tableTemplate');
    const clone = template.content.cloneNode(true);
    const card = clone.querySelector('.table-card');
    const tableId = `table-${tableIdCounter++}`;
    card.dataset.tableId = tableId;

    // Add event listeners
    clone.querySelector('.remove-table-btn').addEventListener('click', function() {
        card.remove();
        updateAllRelationSelects();
    });

    clone.querySelector('.add-column-btn').addEventListener('click', function() {
        addColumn(card);
    });

    clone.querySelector('.table-name').addEventListener('input', function() {
        updatePrimaryKeySelect(card);
        updateAllRelationSelects();
    });

    document.getElementById('tablesContainer').appendChild(clone);
    
    // Add initial column
    addColumn(card);
    
    return card;
}

function addColumn(tableCard) {
    const template = document.getElementById('columnTemplate');
    const clone = template.content.cloneNode(true);
    const columnsContainer = tableCard.querySelector('.columns-container');
    
    clone.querySelector('.remove-column-btn').addEventListener('click', function(e) {
        e.target.closest('.column-row').remove();
        updatePrimaryKeySelect(tableCard);
        updateAllRelationSelects();
    });

    clone.querySelector('.column-name').addEventListener('input', function() {
        updatePrimaryKeySelect(tableCard);
        updateAllRelationSelects();
    });

    clone.querySelector('.column-datatype').addEventListener('change', function(e) {
        const lengthInput = e.target.closest('.column-row').querySelector('.column-length');
        const dataType = e.target.value.toUpperCase();
        
        // Show/hide length field based on data type
        if (dataType === 'VARCHAR' || dataType === 'NVARCHAR' || dataType === 'CHAR' || dataType === 'NCHAR') {
            lengthInput.disabled = false;
            lengthInput.placeholder = 'Length';
        } else {
            lengthInput.disabled = true;
            lengthInput.value = '';
            lengthInput.placeholder = 'N/A';
        }
    });

    columnsContainer.appendChild(clone);
    updatePrimaryKeySelect(tableCard);
}

function updatePrimaryKeySelect(tableCard) {
    const pkSelect = tableCard.querySelector('.primary-key-select');
    const currentValue = pkSelect.value;
    pkSelect.innerHTML = '<option value="">Select a column as primary key</option>';
    
    const columns = tableCard.querySelectorAll('.column-name');
    columns.forEach(col => {
        if (col.value.trim()) {
            const option = document.createElement('option');
            option.value = col.value.trim();
            option.textContent = col.value.trim();
            if (col.value.trim() === currentValue) {
                option.selected = true;
            }
            pkSelect.appendChild(option);
        }
    });
}

function addRelation() {
    const template = document.getElementById('relationTemplate');
    const clone = template.content.cloneNode(true);
    
    // Append first
    document.getElementById('relationsContainer').appendChild(clone);
    
    // Now get the actual card from the DOM
    const card = document.getElementById('relationsContainer').lastElementChild;
    const relationId = `relation-${relationIdCounter++}`;
    card.dataset.relationId = relationId;

    // Add event listeners on the actual DOM element
    card.querySelector('.remove-relation-btn').addEventListener('click', function() {
        card.remove();
    });

    // Update columns when table is selected
    card.querySelector('.parent-table-select').addEventListener('change', function(e) {
        updateColumnSelect(e.target, card.querySelector('.parent-column-select'));
    });

    card.querySelector('.child-table-select').addEventListener('change', function(e) {
        updateColumnSelect(e.target, card.querySelector('.child-column-select'));
    });

    // Now populate the table dropdowns
    updateRelationSelects(card);
}

function updateColumnSelect(tableSelect, columnSelect) {
    columnSelect.innerHTML = '<option value="">Select Column</option>';
    
    const tableName = tableSelect.value;
    if (!tableName) return;

    const tableCard = Array.from(document.querySelectorAll('.table-card')).find(
        card => card.querySelector('.table-name').value === tableName
    );

    if (tableCard) {
        const columns = tableCard.querySelectorAll('.column-name');
        columns.forEach(col => {
            if (col.value.trim()) {
                const option = document.createElement('option');
                option.value = col.value.trim();
                option.textContent = col.value.trim();
                columnSelect.appendChild(option);
            }
        });
    }
}

function updateRelationSelects(relationCard) {
    const parentTableSelect = relationCard.querySelector('.parent-table-select');
    const childTableSelect = relationCard.querySelector('.child-table-select');
    
    const parentValue = parentTableSelect.value;
    const childValue = childTableSelect.value;
    
    parentTableSelect.innerHTML = '<option value="">Select Table</option>';
    childTableSelect.innerHTML = '<option value="">Select Table</option>';

    const tables = document.querySelectorAll('.table-name');
    tables.forEach(tableInput => {
        const tableName = tableInput.value.trim();
        if (tableName) {
            const option1 = document.createElement('option');
            option1.value = tableName;
            option1.textContent = tableName;
            if (tableName === parentValue) option1.selected = true;
            parentTableSelect.appendChild(option1);

            const option2 = document.createElement('option');
            option2.value = tableName;
            option2.textContent = tableName;
            if (tableName === childValue) option2.selected = true;
            childTableSelect.appendChild(option2);
        }
    });
}

function updateAllRelationSelects() {
    document.querySelectorAll('.relation-card').forEach(updateRelationSelects);
}

function collectFormData() {
    const data = {
        databaseName: document.getElementById('databaseName').value.trim(),
        tables: [],
        relations: []
    };

    // Collect tables
    document.querySelectorAll('.table-card').forEach(tableCard => {
        const tableName = tableCard.querySelector('.table-name').value.trim();
        const primaryKey = tableCard.querySelector('.primary-key-select').value;
        
        if (!tableName) return;

        const table = {
            tableName: tableName,
            primaryKey: primaryKey,
            columns: []
        };

        tableCard.querySelectorAll('.column-row').forEach(colRow => {
            const columnName = colRow.querySelector('.column-name').value.trim();
            const dataType = colRow.querySelector('.column-datatype').value;
            const maxLength = colRow.querySelector('.column-length').value;
            const isNullable = colRow.querySelector('.column-nullable').checked;

            if (columnName && dataType) {
                table.columns.push({
                    columnName: columnName,
                    dataType: dataType,
                    maxLength: maxLength ? parseInt(maxLength) : null,
                    isNullable: isNullable
                });
            }
        });

        if (table.columns.length > 0) {
            data.tables.push(table);
        }
    });

    // Collect relations
    document.querySelectorAll('.relation-card').forEach(relationCard => {
        const parentTable = relationCard.querySelector('.parent-table-select').value;
        const parentColumn = relationCard.querySelector('.parent-column-select').value;
        const childTable = relationCard.querySelector('.child-table-select').value;
        const childColumn = relationCard.querySelector('.child-column-select').value;

        if (parentTable && parentColumn && childTable && childColumn) {
            data.relations.push({
                parentTable: parentTable,
                parentColumn: parentColumn,
                childTable: childTable,
                childColumn: childColumn
            });
        }
    });

    return data;
}

async function handleSubmit(e) {
    e.preventDefault();
    
    const data = collectFormData();
    
    if (!validateFormData(data)) {
        return;
    }

    showResult('Creating database...', 'info', false);

    try {
        const response = await fetch('/Database/Create', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(data)
        });

        const result = await response.json();
        
        if (result.success) {
            showResult('✅ Success!', 'success', true, result.message, result.generatedSql);
        } else {
            const errorMessage = result.errors && result.errors.length > 0 
                ? result.errors.join('<br>') 
                : result.message;
            showResult('❌ Error', 'danger', true, errorMessage);
        }
    } catch (error) {
        showResult('❌ Error', 'danger', true, 'Network error: ' + error.message);
    }
}

async function previewSql() {
    const data = collectFormData();
    
    if (!validateFormData(data)) {
        return;
    }

    try {
        const response = await fetch('/Database/Preview', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(data)
        });

        const result = await response.json();
        
        if (result.success) {
            showResult('📝 SQL Preview', 'info', true, '', result.sql);
        } else {
            const errorMessage = result.errors && result.errors.length > 0 
                ? result.errors.join('<br>') 
                : 'Failed to generate SQL preview';
            showResult('❌ Error', 'danger', true, errorMessage);
        }
    } catch (error) {
        showResult('❌ Error', 'danger', true, 'Network error: ' + error.message);
    }
}

function validateFormData(data) {
    if (!data.databaseName) {
        alert('Please enter a database name');
        return false;
    }

    if (data.tables.length === 0) {
        alert('Please add at least one table');
        return false;
    }

    for (const table of data.tables) {
        if (!table.tableName) {
            alert('Please enter a name for all tables');
            return false;
        }
        if (table.columns.length === 0) {
            alert(`Table "${table.tableName}" must have at least one column`);
            return false;
        }
        if (!table.primaryKey) {
            alert(`Please select a primary key for table "${table.tableName}"`);
            return false;
        }
    }

    return true;
}

function showResult(title, type, show, message = '', sql = '') {
    const resultSection = document.getElementById('resultSection');
    const resultTitle = document.getElementById('resultTitle');
    const resultMessage = document.getElementById('resultMessage');
    const sqlPreview = document.getElementById('sqlPreview');
    const sqlCode = document.getElementById('sqlCode');

    resultTitle.textContent = title;
    resultTitle.className = `text-${type}`;
    
    if (message) {
        resultMessage.innerHTML = message;
        resultMessage.className = `alert alert-${type}`;
    } else {
        resultMessage.innerHTML = '';
        resultMessage.className = '';
    }

    if (sql) {
        sqlCode.textContent = sql;
        sqlPreview.classList.remove('d-none');
    } else {
        sqlPreview.classList.add('d-none');
    }

    if (show) {
        resultSection.classList.remove('d-none');
        resultSection.scrollIntoView({ behavior: 'smooth' });
    } else {
        resultSection.classList.add('d-none');
    }
}
