window.initCategorySelects = function (rootElement) {
    const scope = rootElement || document;
    const categorySelectElements = scope.querySelectorAll('select[data-category-select="true"]');

    categorySelectElements.forEach(function (nativeSelect) {
        initializeCategorySelect(nativeSelect);
    });

    function initializeCategorySelect(nativeSelect) {
        if (nativeSelect.dataset.customized === 'true') {
            return;
        }
        nativeSelect.dataset.customized = 'true';

        const wrapper = document.createElement('div');
        wrapper.className = 'category-select-wrapper';

        const triggerButton = document.createElement('button');
        triggerButton.type = 'button';
        triggerButton.className = 'form-select category-select-trigger';
        if (nativeSelect.classList.contains('form-select-sm')) {
            triggerButton.classList.add('form-select-sm');
        }
        triggerButton.setAttribute('aria-haspopup', 'listbox');
        triggerButton.setAttribute('aria-expanded', 'false');

        const dropdownMenu = document.createElement('div');
        dropdownMenu.className = 'category-select-menu';
        dropdownMenu.setAttribute('role', 'listbox');

        nativeSelect.parentNode.insertBefore(wrapper, nativeSelect);
        wrapper.appendChild(nativeSelect);
        wrapper.appendChild(triggerButton);
        wrapper.appendChild(dropdownMenu);

        nativeSelect.classList.add('category-select-native-hidden');

        function renderTriggerContent() {
            const selectedOption = nativeSelect.options[nativeSelect.selectedIndex];
            triggerButton.innerHTML = '';

            if (!selectedOption || !selectedOption.value || selectedOption.value === '0') {
                const placeholderSpan = document.createElement('span');
                placeholderSpan.className = 'text-muted';
                placeholderSpan.textContent = selectedOption ? selectedOption.textContent : 'Wybierz kategorię';
                triggerButton.appendChild(placeholderSpan);
                return;
            }

            const itemContent = buildOptionContent(selectedOption);
            triggerButton.appendChild(itemContent);
        }

        function buildOptionContent(optionElement) {
            const container = document.createElement('span');
            container.className = 'category-select-item-content';

            const iconClass = optionElement.getAttribute('data-icon');
            const categoryType = optionElement.getAttribute('data-type');

            if (iconClass) {
                const iconElement = document.createElement('i');
                const colorClass = categoryType === 'Income' ? 'text-success' : 'text-danger';
                iconElement.className = iconClass + ' ' + colorClass + ' category-select-icon';
                container.appendChild(iconElement);
            }

            const labelSpan = document.createElement('span');
            labelSpan.textContent = optionElement.textContent;
            container.appendChild(labelSpan);

            return container;
        }

        function buildMenuOptions() {
            dropdownMenu.innerHTML = '';

            Array.from(nativeSelect.options).forEach(function (optionElement) {
                if (!optionElement.value || optionElement.value === '0') {
                    return;
                }

                const optionButton = document.createElement('button');
                optionButton.type = 'button';
                optionButton.className = 'category-select-option';
                optionButton.setAttribute('role', 'option');
                optionButton.dataset.value = optionElement.value;

                if (optionElement.selected) {
                    optionButton.classList.add('is-selected');
                    optionButton.setAttribute('aria-selected', 'true');
                }

                optionButton.appendChild(buildOptionContent(optionElement));

                optionButton.addEventListener('click', function () {
                    nativeSelect.value = optionElement.value;
                    nativeSelect.dispatchEvent(new Event('change', { bubbles: true }));
                    updateSelectedState();
                    closeMenu();
                });

                dropdownMenu.appendChild(optionButton);
            });
        }

        function updateSelectedState() {
            renderTriggerContent();
            const currentValue = nativeSelect.value;

            dropdownMenu.querySelectorAll('.category-select-option').forEach(function (optionBtn) {
                const isSelected = optionBtn.dataset.value === currentValue;
                optionBtn.classList.toggle('is-selected', isSelected);
                optionBtn.setAttribute('aria-selected', isSelected ? 'true' : 'false');
            });
        }

        function openMenu() {
            document.querySelectorAll('.category-select-wrapper.is-open').forEach(function (openWrapper) {
                if (openWrapper !== wrapper) {
                    openWrapper.classList.remove('is-open');
                    const openTrigger = openWrapper.querySelector('.category-select-trigger');
                    if (openTrigger) {
                        openTrigger.setAttribute('aria-expanded', 'false');
                    }
                }
            });

            wrapper.classList.add('is-open');
            triggerButton.setAttribute('aria-expanded', 'true');
        }

        function closeMenu() {
            wrapper.classList.remove('is-open');
            triggerButton.setAttribute('aria-expanded', 'false');
        }

        triggerButton.addEventListener('click', function (event) {
            event.stopPropagation();
            if (wrapper.classList.contains('is-open')) {
                closeMenu();
            } else {
                openMenu();
            }
        });

        nativeSelect.addEventListener('change', updateSelectedState);

        document.addEventListener('click', function (event) {
            if (!wrapper.contains(event.target)) {
                closeMenu();
            }
        });

        document.addEventListener('keydown', function (event) {
            if (event.key === 'Escape' && wrapper.classList.contains('is-open')) {
                closeMenu();
            }
        });

        buildMenuOptions();
        updateSelectedState();
    }
};

document.addEventListener('DOMContentLoaded', function () {
    window.initCategorySelects(document);
});