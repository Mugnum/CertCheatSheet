/** @type {HTMLDialogElement} Окно восстановления соединения Blazor. */
const reconnectModal = document.getElementById("components-reconnect-modal");
/** @type {HTMLButtonElement} Кнопка ручного переподключения. */
const retryButton = document.getElementById("components-reconnect-button");
/** @type {HTMLButtonElement} Кнопка возобновления приостановленной сессии. */
const resumeButton = document.getElementById("components-resume-button");

reconnectModal.addEventListener("components-reconnect-state-changed", handleReconnectStateChanged);
retryButton.addEventListener("click", retryConnection);
resumeButton.addEventListener("click", resumeSession);

/**
 * Обновляет окно при смене состояния подключения.
 * @param {CustomEvent<{state: string}>} event Состояние, опубликованное Blazor.
 * @returns {void}
 */
function handleReconnectStateChanged(event) {
	if (event.detail.state === "show") {
		reconnectModal.showModal();
	} else if (event.detail.state === "hide") {
		reconnectModal.close();
		document.removeEventListener("visibilitychange", retryWhenVisible);
	} else if (event.detail.state === "failed") {
		document.addEventListener("visibilitychange", retryWhenVisible);
	} else if (event.detail.state === "rejected") {
		location.reload();
	}
}

/**
 * Переподключается к серверу; создаёт новую страницу, если сессия уже утрачена.
 * @returns {Promise<void>} Завершение попытки восстановления.
 */
async function retryConnection() {
	document.removeEventListener("visibilitychange", retryWhenVisible);
	try {
		const connected = await Blazor.reconnect();
		if (connected) {
			reconnectModal.close();
		} else {
			const resumed = await Blazor.resumeCircuit();
			if (resumed) {
				reconnectModal.close();
			} else {
				location.reload();
			}
		}
	} catch (error) {
		console.error("Не удалось восстановить соединение с сервером.", error);
		document.addEventListener("visibilitychange", retryWhenVisible);
	}
}

/**
 * Возобновляет приостановленную сервером сессию.
 * @returns {Promise<void>} Завершение попытки возобновления.
 */
async function resumeSession() {
	try {
		const resumed = await Blazor.resumeCircuit();
		if (resumed) {
			reconnectModal.close();
		} else {
			location.reload();
		}
	} catch (error) {
		console.error("Не удалось возобновить сессию.", error);
		reconnectModal.classList.replace("components-reconnect-paused", "components-reconnect-resume-failed");
	}
}

/**
 * Повторяет подключение после возвращения пользователя во вкладку.
 * @returns {Promise<void>} Завершение попытки, если вкладка видима.
 */
async function retryWhenVisible() {
	if (document.visibilityState === "visible") {
		await retryConnection();
	}
}
